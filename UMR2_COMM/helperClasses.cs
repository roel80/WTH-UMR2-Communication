using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KluitNET.WTH.UMR2
{
    /*
     *  (C)2023 Roeland Kluit
     *  
     *  This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
     *  This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
     *  You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
     * 
     *  Communication between WTH UMR2 and smart home software
     *  
     *  Requires WTH UMR2 connected to the same network
     *  - For fanlink devices UMR C820S1 - firmware 1.3 or later is required. See P9 for version
     *  - RF and other devices are not implemented. I do not have them.
     * 
     */

    internal static class STATIC
    {
        public static double ECO_TEMPERATURE = 16.0;
        public static double OFF_TEMPERATURE = 8.0;
        public static string FANLINK = "fanlink";
        public static string UMR_URL = "umr_2";
        public static uint RERESH_INTERVAL = 60;
    }

    public enum UMRState
    {
        UMR_IDLE = 0,
        UMR_HEATING,
        UMR_COOLING
    }

    public enum ValveID
    {
        ID0_TSID1_VALVE1 = 0,
        ID1_TSID2_VALVE2,
        ID2_TSID3_VALVE3,
        ID3_TSID4_VALVE4,
        ID4_TSID5_VALVE5,
        ID5_TSID6_VALVE6,
        ID6_TSID7_VALVE7,
        ID7_TSID8_VALVE8,
        ID8_TSID9_RESERVED9,
        ID9_TSID10_RESERVED10
    }

    public class Channel
    {
        public int index { get; set; }
        public int label { get; set; }
        public string heating { get; set; }
        public string cooling { get; set; }
        public int heatFactor { get; set; }
        public int coolFactor { get; set; }
        public int pwmFactor { get; set; }
        public string buffer { get; set; }
    }

    internal class ThermostatConfig
    {
        public int index { get; set; }
        public string select { get; set; }
        public int label { get; set; }
        public string mode { get; set; }
        public string serialNumber { get; set; }
    }

    public class Cooler
    {
        public string state { get; set; }
        public int factor { get; set; }
    }

    public class Heater
    {
        public string state { get; set; }
        public int factor { get; set; }
    }

    public class Pump
    {
        public int speed { get; set; }
    }

    public class valve
    {
        public int index { get; set; }
        public int state { get; set; }
    }

    public class Valf
    {
        public int index { get; set; }
        public string mode { get; set; }
    }

    public class FanlinkDevice
    {
        public int index { get; set; }
        public string type { get; set; }
        public string serialNumber { get; set; }
        public object lastSeen { get; set; }
        public object lastLearn { get; set; }
    }

    internal class UMRStatusInternal
    {
        public Heater heater { get; set; }
        public Cooler cooler { get; set; }
        public Pump pump { get; set; }
        public List<valve> valves { get; set; }
        public List<ThermostatInternal> Thermostats { get; set; }
        //public List<FanlinkDevice> FanlinkDevices { get; set; }
    }

    internal class ThermostatInternal
    {
        public int index { get; set; }
        public int factor { get; set; }
        public double temperature { get; set; }
        public double setpoint { get; set; }
    }

    internal class UMRConfig
    {
        public List<ThermostatConfig> ThermostatConfigs { get; set; }
        public List<Valf> OutputValveConfig { get; set; }
        public List<Channel> Channels { get; set; }
    }

    internal class UMR2internal
    {
        internal UMRStatusInternal umrStatus = new UMRStatusInternal();
        internal UMRConfig umrConfig = new UMRConfig();
    }

    public class UMRChannel
    {
        public override string ToString()
        {
            return "Valve: " + valveindex.ToString() + " - " + valveState;
        }

        internal UMRChannel(UMR2Controller parent)
        {
            this.parent = parent;
        }
        private UMR2Controller parent;
        public ValveID valveindex { get; internal set; }
        public int valveState { get; internal set; }
        public UMR2Controller.UMRThermostat Thermostat { get; internal set; }

        public bool isActive
        {
            get
            {
                return valveState != 0;
            }
        }
    }
}
