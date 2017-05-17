using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.HoloToolkit.SpatialMapping.Scripts
{
    public class Helper
    {
        public enum DebugType{ ValueCheck, Weird ,Error}

        public void debug(String line, DebugType type)
        {
            string msgPrefix = null;
            if (type == DebugType.ValueCheck)
            {
                msgPrefix = "<color=green>ValueCheck:</color>";
            }
            else if (type == DebugType.Weird)
            {
                msgPrefix = "<color=orange>Weird:</color>";
            }
            else if (type == DebugType.Error)
            {
                msgPrefix = "<color=red>Error:</color>";
            }

            if (msgPrefix != null)
            {
                Debug.Log(msgPrefix);
                Debug.Log(line)
            }
           
        }
    }
}
