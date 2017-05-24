// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;

namespace HoloToolkit.Unity.SpatialMapping
{
    /// <summary>
    /// Spatial Mapping Observer states.
    /// </summary>
    public enum ObserverStates
    {
        /// <summary>
        /// The SurfaceObserver is currently running.
        /// </summary>
        Running = 0,

        /// <summary>
        /// The SurfaceObserver is currently idle.
        /// </summary>
        Stopped = 1
    }

    /// <summary>
    /// The SpatialMappingObserver class encapsulates the SurfaceObserver into an easy to use
    /// object that handles managing the observed surfaces and the rendering of surface geometry.
    /// </summary>
    public class SpatialMappingObserver : SpatialMappingSource
    {
        [Tooltip("The number of triangles to calculate per cubic meter.")]
        public float TrianglesPerCubicMeter = 300f;

        [Tooltip("The extents of the observation volume.")]
        public Vector3 Extents = Vector3.one * 2.0f;

        [Tooltip("How long to wait (in sec) between Spatial Mapping updates.")]
        public float TimeBetweenUpdates = 1.5f;

        [Tooltip("How long to wait (in sec) at the start of the app")]
        public float TimeBeforeStartMapping = 5.0f;

        public int surfaceAddCount = 0;
        public int surfaceUpdateCount = 0;
        public int surfaceRemoveCount = 0;

        /// <summary>
        /// Indicates the current state of the Surface Observer.
        /// </summary>
        public ObserverStates ObserverState { get; private set; }

        /// <summary>
        /// Our Surface Observer object for generating/updating Spatial Mapping data.
        /// </summary>
        private SurfaceObserver observer;

        /// <summary>
        /// A queue of surfaces that need their meshes created (or updated).
        /// </summary>
        private readonly Queue<SurfaceId> surfaceWorkQueue = new Queue<SurfaceId>();

        /// <summary>
        /// To prevent too many meshes from being generated at the same time, we will
        /// only request one mesh to be created at a time.  This variable will track
        /// if a mesh creation request is in flight.
        /// </summary>
        private SurfaceObject? outstandingMeshRequest = null;

        /// <summary>
        /// When surfaces are replaced or removed, rather than destroying them, we'll keep
        /// one as a spare for use in outstanding mesh requests. That way, we'll have fewer
        /// game object create/destroy cycles, which should help performance.
        /// </summary>
        private SurfaceObject? spareSurfaceObject = null;

        /// <summary>
        /// Used to track when the Observer was last updated.
        /// </summary>
        private float updateTime;

        private float awakeTime;

        protected override void Awake()
        {
            base.Awake();

            ObserverState = ObserverStates.Stopped;

            awakeTime = Time.unscaledTime;
        }

        private bool IsReadyForStart()
        {
            return Time.unscaledTime - awakeTime > TimeBeforeStartMapping;
        }

        private void SetRandomMeshColor(SurfaceObject surface)
        {
            //  _WireColor("Wire color", Color) = (1.0, 1.0, 1.0, 1.0)
            // Pick a random, saturated and not-too-dark color
            var randomColor = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
            surface.Renderer.material.SetColor("_WireColor", randomColor);
        }

        /// <summary>
        /// Called once per frame.
        /// </summary>
        private void Update()
        {
            if ((ObserverState == ObserverStates.Running) && (outstandingMeshRequest == null))
            {
                //if (!IsReadyForStart())
                //{
                //    surfaceWorkQueue.Clear();
                //}
                if (surfaceWorkQueue.Count > 0)
                {
                    // We're using a simple first-in-first-out rule for requesting meshes, but a more sophisticated algorithm could prioritize
                    // the queue based on distance to the user or some other metric.
                    SurfaceId surfaceID = surfaceWorkQueue.Dequeue();
                    
                    string surfaceName = ("Surface-" + surfaceID.handle);

                    SurfaceObject newSurface;
                    WorldAnchor worldAnchor;
                    
                    if (spareSurfaceObject == null)
                    {
                        Debug.Log(System.String.Format("Get new for surfaceId: {0}", surfaceID.handle));
                        newSurface = CreateSurfaceObject(
                            mesh: null,
                            objectName: surfaceName,
                            parentObject: transform,
                            meshID: surfaceID.handle,
                            drawVisualMeshesOverride: false
                            );

                        worldAnchor = newSurface.Object.AddComponent<WorldAnchor>();
                    }
                    else
                    {
                        Debug.Log(System.String.Format("Get spare for surfaceId: {0}", surfaceID.handle));
                        newSurface = spareSurfaceObject.Value;
                        spareSurfaceObject = null;

                        Debug.Assert(!newSurface.Object.activeSelf);
                        newSurface.Object.SetActive(true);

                        Debug.Assert(newSurface.Filter.sharedMesh == null);
                        Debug.Assert(newSurface.Collider.sharedMesh == null);
                        newSurface.Object.name = surfaceName;
                        Debug.Assert(newSurface.Object.transform.parent == transform);
                        newSurface.ID = surfaceID.handle;
                        newSurface.Renderer.enabled = false;

                        worldAnchor = newSurface.Object.GetComponent<WorldAnchor>();
                        Debug.Assert(worldAnchor != null);
                    }

                    var surfaceData = new SurfaceData(
                        surfaceID,
                        newSurface.Filter,
                        worldAnchor,
                        newSurface.Collider,
                        TrianglesPerCubicMeter,
                        _bakeCollider: true
                        );
                    Debug.Log("observer.RequestMeshAsync(surfaceData, SurfaceObserver_OnDataReady)");
                    if (observer.RequestMeshAsync(surfaceData, SurfaceObserver_OnDataReady))
                    {
                        SetRandomMeshColor(newSurface);
                        outstandingMeshRequest = newSurface;
                    }
                    else
                    {
                        Debug.LogErrorFormat("Mesh request for failed. Is {0} a valid Surface ID?", surfaceID.handle);

                        Debug.Assert(outstandingMeshRequest == null);
                        ReclaimSurface(newSurface);
                    }
                }
                else if ((Time.unscaledTime - updateTime) >= TimeBetweenUpdates)
                {
                    Debug.Log("observer.Update(SurfaceObserver_OnSurfaceChanged);");
                    observer.Update(SurfaceObserver_OnSurfaceChanged);
                    updateTime = Time.unscaledTime;
                }
            }
        }

        /// <summary>
        /// Starts the Surface Observer.
        /// </summary>
        public void StartObserving()
        {
            if (observer == null)
            {
                observer = new SurfaceObserver();
                //Vector3 sceneOrigin = Camera.main.transform.position;
                //observer.SetVolumeAsAxisAlignedBox(sceneOrigin, Extents);
                observer.SetVolumeAsAxisAlignedBox(Vector3.zero, Extents);
            }

            if (ObserverState != ObserverStates.Running)
            {
                Debug.Log("Starting the observer.");
                ObserverState = ObserverStates.Running;

                // We want the first update immediately.
                updateTime = 0;
            }
        }

        public void UpdateObserver()
        {
            
            var newObserver = new SurfaceObserver();
            Vector3 sceneOrigin = Camera.main.transform.position;
            newObserver.SetVolumeAsAxisAlignedBox(sceneOrigin, Extents);
            CleanupObserver();
            GC.Collect();
            observer = newObserver;
            ObserverState = ObserverStates.Running;
            Debug.Log("UpdateObserver() new one starts");
            // We want the first update immediately.
            updateTime = 0;
        }

        /// <summary>
        /// Stops the Surface Observer.
        /// </summary>
        /// <remarks>Sets the Surface Observer state to ObserverStates.Stopped.</remarks>
        public void StopObserving()
        {
            if (ObserverState == ObserverStates.Running)
            {
                Debug.Log("Stopping the observer.");
                ObserverState = ObserverStates.Stopped;

                surfaceWorkQueue.Clear();
                updateTime = 0;
            }
        }

        /// <summary>
        /// Cleans up all memory and objects associated with the observer.
        /// </summary>
        public void CleanupObserver()
        {
            StopObserving();

            if (observer != null)
            {
                observer.Dispose();
                observer = null;
            }

            if (outstandingMeshRequest != null)
            {
                CleanUpSurface(outstandingMeshRequest.Value);
                outstandingMeshRequest = null;
            }

            if (spareSurfaceObject != null)
            {
                CleanUpSurface(spareSurfaceObject.Value);
                spareSurfaceObject = null;
            }

            Cleanup();
        }

        internal void CleanupPartiallyAfterSend()
        {
            Debug.Log(System.String.Format("ADDED: {0}\t UPDATED: {1}\t REMOVED : {2} ", surfaceAddCount, surfaceUpdateCount, surfaceRemoveCount));
            CleanupFarAfterSend();
            surfaceAddCount = 0;
            surfaceUpdateCount = 0;
            surfaceRemoveCount = 0;
        }

        internal void CleanupAllAfterSend()
        {
            Debug.Log(System.String.Format("ADDED: {0}\t UPDATED: {1}\t REMOVED : {2} ", surfaceAddCount, surfaceUpdateCount, surfaceRemoveCount));
            Cleanup();
            surfaceAddCount = 0;
            surfaceUpdateCount = 0;
            surfaceRemoveCount = 0;
        }

        /// <summary>
        /// Can be called to override the default origin for the observed volume.  Can only be called while observer has been started.
        /// </summary>
        public bool SetObserverOrigin(Vector3 origin)
        {
            bool originUpdated = false;

            if (observer != null)
            {
                observer.SetVolumeAsAxisAlignedBox(origin, Extents);
                originUpdated = true;
            }

            return originUpdated;
        }

        /// <summary>
        /// Handles the SurfaceObserver's OnDataReady event.
        /// </summary>
        /// <param name="cookedData">Struct containing output data.</param>
        /// <param name="outputWritten">Set to true if output has been written.</param>
        /// <param name="elapsedCookTimeSeconds">Seconds between mesh cook request and propagation of this event.</param>
        private void SurfaceObserver_OnDataReady(SurfaceData cookedData, bool outputWritten, float elapsedCookTimeSeconds)
        {
            Debug.Log("Data is ready");
            if (outstandingMeshRequest == null)
            {
                Debug.LogErrorFormat("Got OnDataReady for surface {0} while no request was outstanding.",
                    cookedData.id.handle
                    );

                return;
            }

            if (!IsMatchingSurface(outstandingMeshRequest.Value, cookedData))
            {
                Debug.LogErrorFormat("Got mismatched OnDataReady for surface {0} while request for surface {1} was outstanding.",
                    cookedData.id.handle,
                    outstandingMeshRequest.Value.ID
                    );

                ReclaimSurface(outstandingMeshRequest.Value);
                outstandingMeshRequest = null;

                return;
            }

            if (ObserverState != ObserverStates.Running)
            {
                Debug.LogFormat("Got OnDataReady for surface {0}, but observer was no longer running.",
                    cookedData.id.handle
                    );

                ReclaimSurface(outstandingMeshRequest.Value);
                outstandingMeshRequest = null;

                return;
            }

            if (!outputWritten)
            {
                ReclaimSurface(outstandingMeshRequest.Value);
                outstandingMeshRequest = null;

                return;
            }

            Debug.Assert(outstandingMeshRequest.Value.Object.activeSelf);
            //Debug.Log(System.String.Format("SpatialMappingManager.Instance.DrawVisualMeshes == {0}", SpatialMappingManager.Instance.DrawVisualMeshes));
            outstandingMeshRequest.Value.Renderer.enabled = SpatialMappingManager.Instance.DrawVisualMeshes;

            SurfaceObject? replacedSurface = UpdateOrAddSurfaceObject(outstandingMeshRequest.Value, destroyGameObjectIfReplaced: false);
            outstandingMeshRequest = null;

            if (replacedSurface != null)
            {
                ReclaimSurface(replacedSurface.Value);
            }
        }

        /// <summary>
        /// Handles the SurfaceObserver's OnSurfaceChanged event.
        /// </summary>
        /// <param name="id">The identifier assigned to the surface which has changed.</param>
        /// <param name="changeType">The type of change that occurred on the surface.</param>
        /// <param name="bounds">The bounds of the surface.</param>
        /// <param name="updateTime">The date and time at which the change occurred.</param>
        private void SurfaceObserver_OnSurfaceChanged(SurfaceId id, SurfaceChange changeType, Bounds bounds, DateTime updateTime)
        {
            // Verify that the client of the Surface Observer is expecting updates.
            if (ObserverState != ObserverStates.Running)
            {
                return;
            }

            switch (changeType)
            {
                /// Interestingly, surface is pushed to queue only when it's updated but not added.
                
                case SurfaceChange.Added:
                    //Debug.Log(System.String.Format("SurfaceObserver_OnSurfaceChanged:    ADDED id: {0}", id));
                    if (!isSurfaceObsolete(id))
                    {
                        surfaceAddCount += 1;
                        surfaceWorkQueue.Enqueue(id);
                    }
                    break;
                case SurfaceChange.Updated:
                    //if (! isSurfaceObsolete(id) || SpatialMappingManager.Instance.isSurfaceNearCamera(bounds))
                    //{
                    //    //surfaceWorkQueue.Enqueue(id);
                    //    //Debug.Log(System.String.Format("SurfaceObserver_OnSurfaceChanged:    UPDATED id: {0}", id));
                    //    surfaceUpdateCount += 1;

                    //}
                    surfaceUpdateCount += 1;
                    break;

                case SurfaceChange.Removed:
                    //Debug.Log(System.String.Format("SurfaceObserver_OnSurfaceChanged:    REMOVED id: {0}", id));
                    surfaceRemoveCount += 1;
                    //SurfaceObject? removedSurface = RemoveSurfaceIfFound(id.handle, destroyGameObject: false);
                    //if (removedSurface != null)
                    //{
                    //    ReclaimSurface(removedSurface.Value);
                    //}
                    break;

                default:
                    Debug.LogErrorFormat("Unexpected {0} value: {1}.", changeType.GetType(), changeType);
                    break;
            }
        }

        /// <summary>
        /// Called when the GameObject is unloaded.
        /// </summary>
        private void OnDestroy()
        {
            CleanupObserver();
        }

        private void ReclaimSurface(SurfaceObject availableSurface)
        {
            //if (spareSurfaceObject == null)
            //{
            //    CleanUpSurface(availableSurface, destroyGameObject: false);

            //    availableSurface.Object.name = "Unused Surface";
            //    availableSurface.Object.SetActive(false);

            //    spareSurfaceObject = availableSurface;
            //}
            //else
            //{
            //    CleanUpSurface(availableSurface);
            //}
            CleanUpSurface(availableSurface);
        }

        private bool IsMatchingSurface(SurfaceObject surfaceObject, SurfaceData surfaceData)
        {
            return (surfaceObject.ID == surfaceData.id.handle)
                && (surfaceObject.Filter == surfaceData.outputMesh)
                && (surfaceObject.Collider == surfaceData.outputCollider)
                ;
        }

        public bool isSurfaceObsolete(SurfaceId id)
        {
            return SpatialMappingManager.Instance.obsoleteSurfaceIds.Contains(id.handle);
        }

        
    }
}
