﻿//   FormatUtils.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2019 Allis Tauri

using UnityEngine;

namespace AT_Utils.UI
{
    public static class FormatUtils
    {
        public static string formatVeryBigValue(float value, string unit, string format = "F1")
        {
            string mod = "";
            if(value > 1e24) { value /= 1e24f; mod = "Y"; }
            else if(value > 1e21) { value /= 1e21f; mod = "Z"; }
            else if(value > 1e18) { value /= 1e18f; mod = "E"; }
            else if(value > 1e15) { value /= 1e15f; mod = "P"; }
            else if(value > 1e12) { value /= 1e12f; mod = "T"; }
            else return formatBigValue(value, unit, format);
            return value.ToString(format) + mod + unit;
        }

        public static string formatBigValue(float value, string unit, string format = "F1")
        {
            string mod = "";
            if(value > 1e9) { value /= 1e9f; mod = "G"; }
            else if(value > 1e6) { value /= 1e6f; mod = "M"; }
            else if(value > 1e3) { value /= 1e3f; mod = "k"; }
            return value.ToString(format) + mod + unit;
        }

        public static string formatSmallValue(float value, string unit, string format = "F1")
        {
            string mod = "";
            if(value < 1)
            {
                if(value > 1e-3) { value *= 1e3f; mod = "m"; }
                else if(value > 1e-6) { value *= 1e6f; mod = "μ"; }
                else if(value > 1e-9) { value *= 1e9f; mod = "n"; }
            }
            return value.ToString(format) + mod + unit;
        }

        public static string formatMass(float mass)
        {
            if(mass >= 0.1f)
                return mass.ToString("n2") + "t";
            if(mass >= 0.001f)
                return (mass * 1e3f).ToString("n1") + "kg";
            return (mass * 1e6f).ToString("n0") + "g";
        }

        public static string formatVolume(double volume)
        {
            if(volume < 1f)
                return (volume * 1e3f).ToString("n0") + "L";
            return volume.ToString("n1") + "m3";
        }

        public static string formatUnits(float units)
        {
            units = Mathf.Abs(units);
            if(units >= 1f)
                return units.ToString("n2") + "u";
            if(units >= 1e-3f)
                return (units * 1e3f).ToString("n1") + "mu";
            if(units >= 1e-6f)
                return (units * 1e6f).ToString("n1") + "μu";
            if(units >= 1e-9f)
                return (units * 1e9f).ToString("n1") + "nu";
            if(units >= 1e-13f) //to fully use the last digit 
                return (units * 1e12f).ToString("n1") + "pu";
            return "0.0u"; //effectivly zero
        }

        public static string formatDimensions(Vector3 size)
        { return string.Format("{0:F2}m x {1:F2}m x {2:F2}m", size.x, size.y, size.z); }
    }
}
