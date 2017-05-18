using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HoloToolkit.Unity.SpatialMapping
{
    public static class Helper
    {
        public enum DebugType{ ValueCheck, Weird ,Error}

        public static void debug(object msg, DebugType type = DebugType.ValueCheck)
        {
            string msgPrefix = null;
            if (type == DebugType.ValueCheck)
            {
                msgPrefix = "ValueCheck:\n\t";
            }
            else if (type == DebugType.Weird)
            {
                // orange = #ffa500ff
                msgPrefix = "Something's Weird:\n\t";
            }
            else if (type == DebugType.Error)
            {
                msgPrefix = "Error:\n\t";
            }

            if (msgPrefix != null)
            {
                //Debug.Log(msgPrefix);
                Debug.Log(msgPrefix + msg);
            }
           
        }
    }
}
