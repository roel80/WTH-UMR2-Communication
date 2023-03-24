using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

    public class UMR2Controller
    {
        public delegate void EventOnThermostatEvent(UMR2Controller caller, UMRThermostat thermostat);
        public event EventOnThermostatEvent OnTemperatureChange;
        public event EventOnThermostatEvent OnSetPointChange;
        public event EventOnThermostatEvent OnActiveChange;

        public List<UMRChannel> Channels = new List<UMRChannel>();
        public List<UMRThermostat> Thermostats = new List<UMRThermostat>();

        public uint refreshUMRStatusSeconds = STATIC.RERESH_INTERVAL;
        private string UMR2url = STATIC.UMR_URL;
        private WebClient getclient = new WebClient();
        private WebClient postclient = new WebClient();
        private System.Threading.Timer tmrUpdate = null;

        public int factor { get; private set; }
        private UMR2internal umr2 = new UMR2internal();
        private UMRState _umr_state = UMRState.UMR_IDLE;

        public class UMRThermostat
        {
            private UMR2Controller parent;
            private double temperature;
            private double setpoint;
            private double newsetpoint;
            private bool isActive;
            public int thermostatindex { get; internal set; }
            public string thermostatType { get; internal set; }
            //public DateTime fanlinkLastSeen { get; internal set; }
            //public DateTime temperatureLastSet { get; private set; }
            private double lastSetPointValue { get; set; }
            public List<UMRChannel> connectedChannels { get; internal set; }

            public override string ToString()
            {
                return "Termostat: " + thermostatindex.ToString() + " - " + thermostatType;
            }

            internal UMRThermostat(UMR2Controller parent)
            {
                this.parent = parent;
                isActive = false;
                setpoint = 0;
                newsetpoint = 0;
                temperature = 0;
                lastSetPointValue = 19.0;
            }

            public void OverrideLastOnsetPoint(double temperature)
            {
                if (temperature == STATIC.ECO_TEMPERATURE || temperature == STATIC.OFF_TEMPERATURE || temperature < 5 || temperature > 35)
                {
                    //Ignore
                    return;
                }
                else
                {
                    lastSetPointValue = temperature;
                }
            }

            public void SetPointTemperature(double newValue)
            {
                if (newValue > 4 && newValue < 35)
                {
                    if ((newValue != Setpoint) || (newsetpoint != newValue))
                    {
                        Console.WriteLine(this.ToString() + " Set " + newValue);
                        if (parent.updateTemperature(this, newValue))
                        {
                            newsetpoint = newValue;
                            //temperatureLastSet = DateTime.UtcNow;
                        }
                    }
                }
            }

            public double Temperature
            {
                get => temperature;
                internal set
                {
                    if (temperature != value)
                    {
                        temperature = value;
                        parent.OnTemperatureChange?.Invoke(parent, this);
                    }
                }
            }

            public double Setpoint
            {
                get => setpoint;
                internal set
                {
                    newsetpoint = value;
                    if (setpoint != value)
                    {
                        setpoint = value;
                        parent.OnSetPointChange?.Invoke(parent, this);
                    }
                }
            }

            public bool IsHeating
            {
                get
                {
                    if (parent.State == UMRState.UMR_HEATING && IsActive)
                        return true;
                    return false;
                }
            }

            public bool IsCooling
            {
                get
                {
                    if (parent.State == UMRState.UMR_COOLING && IsActive)
                        return true;
                    return false;
                }
            }

            internal void InternalUMRModeChanged()
            {
                parent.OnActiveChange?.Invoke(parent, this);
            }

            public bool IsActive
            {
                get => isActive;
                internal set
                {
                    if (isActive != value)
                    {
                        isActive = value;
                        parent.OnActiveChange?.Invoke(parent, this);
                    }
                }
            }

            public bool isFanlinkThermostat
            {
                get
                {
                    return thermostatType == STATIC.FANLINK;
                }
            }

            public bool IsEcoMode
            {
                get
                {
                    return Setpoint == STATIC.ECO_TEMPERATURE;
                }
                set
                {
                    if (value == true)
                    {
                        if (!IsOff && !IsEcoMode) //Store setpoint only if not eco or off
                        {
                            if (Setpoint != STATIC.ECO_TEMPERATURE && Setpoint != STATIC.OFF_TEMPERATURE)
                            {
                                lastSetPointValue = Setpoint;
                            }
                        }
                        if (!IsEcoMode)//Only turn on eco if not allready in eco
                        {
                            SetPointTemperature(STATIC.ECO_TEMPERATURE);
                        }
                    }
                    else
                    {
                        if (IsEcoMode) //Only turn on if eco
                        {
                            SetPointTemperature(lastSetPointValue);
                        }
                    }
                }
            }

            public bool IsOn
            {
                get
                {
                    return !IsOff;
                }
                set
                {
                    IsOff = !value;
                }
            }

            public bool IsOff
            {
                get
                {
                    return Setpoint == STATIC.OFF_TEMPERATURE;
                }
                set
                {
                    if (value == true) //Turn off
                    {
                        if (!IsOff && !IsEcoMode) //Store setpoint only if not eco or off
                        {
                            if (Setpoint != STATIC.ECO_TEMPERATURE && Setpoint != STATIC.OFF_TEMPERATURE)
                            {
                                lastSetPointValue = Setpoint;
                            }
                        }
                        if (!IsOff)//Only turn off if on or eco
                        {
                            SetPointTemperature(STATIC.OFF_TEMPERATURE);
                        }
                    }
                    else //Turn on device again
                    {
                        if (IsOff || IsEcoMode) //Only turn on if off or was in EcoMode
                        {
                            SetPointTemperature(lastSetPointValue);
                        }
                    }
                }
            }
        }

        public UMRState State
        {
            get => _umr_state;
            private set
            {
                if(_umr_state != value)
                {
                    _umr_state = value;
                    foreach (var t in this.Thermostats)
                    {
                        t.InternalUMRModeChanged();
                    }
                }
            }
        }

        private UMR2Controller()
        {
        }

        private void TimerUpdate(object state)
        {
            try
            {
                UpdateUMRValues();
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to refresh: " + e.ToString());
            }
        }

        public UMR2Controller(string UMR2url = "umr_2")
        {
            this.UMR2url = UMR2url;
        }

        public bool StartComms()
        {
            if (tmrUpdate == null)
            {
                if (DownloadConfig())
                {
                    if (UpdateUMRValues())
                    {
                        tmrUpdate = new System.Threading.Timer(TimerUpdate, null, 0, refreshUMRStatusSeconds * 1000);
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Unable to retrieve Values");
                    }
                }
                else
                {
                    Console.WriteLine("Unable to retrieve config");                    
                }
                return false;
            }
            return true;
        }

            public void StopComms()
        {
            tmrUpdate = null;
        }

        /*public static UMR2Controller FromJsonString(string URMstring)
        {
            UMR2Controller ret = new UMR2Controller();
            ret.umr2 = (UMR2internal)JsonConvert.DeserializeObject(URMstring, typeof(UMR2internal));
            ret.RefreshStructs();
            return ret;
        }

        public string Export()
        {
            return JsonConvert.SerializeObject(umr2, Formatting.None);
        }*/

        /*public FanlinkDevice FindByserialNumber(string Serial)
        {
            foreach (var dev in umr2.umrStatus.FanlinkDevices)
            {
                if (dev.serialNumber == Serial)
                    return dev;
            }
            return null;
        }*/

        internal bool updateTemperature(UMRThermostat thermostat, double newTemperature)
        {
            if (newTemperature >= 5 && newTemperature < 35)
            {
                try
                {
                    string myParameters = "{\"status\":{\"process\":{\"thermostats\":[{\"index\":" + thermostat.thermostatindex + ",\"setpoint\":" + newTemperature.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "}]}}}";
                    string HtmlResult = postclient.UploadString("http://" + UMR2url + "/set_config.cgi", "POST", myParameters);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                Console.WriteLine("Invalid Temperature: " + newTemperature);
                return false;
            }
        }

        private void updateThermostat(UMRThermostat thermostat, UMRChannel channelStatus)
        {
            bool found = false;
            foreach (var chan in thermostat.connectedChannels)
            {
                if (chan == channelStatus)
                {
                    found = true;
                }
            }
            if (!found)
                thermostat.connectedChannels.Add(channelStatus);

            if (thermostat.thermostatType == STATIC.FANLINK)
            {
                /*var fanlink = FindByserialNumber(umr2.umrConfig.ThermostatConfigs[thermostat.thermostatindex].serialNumber);
                if (fanlink != null)
                {
                    thermostat.fanlinkLastSeen = (DateTime)fanlink.lastSeen;
                }*/
            }
            thermostat.Setpoint = umr2.umrStatus.Thermostats[thermostat.thermostatindex].setpoint;
            thermostat.Temperature = umr2.umrStatus.Thermostats[thermostat.thermostatindex].temperature;
            thermostat.IsActive = umr2.umrStatus.Thermostats[thermostat.thermostatindex].factor != 0;
        }

        private UMRThermostat GetThermostat(int thermostatindex, UMRChannel channel)
        {
            foreach (var thermostat in Thermostats)
            {
                if (thermostat.thermostatindex == thermostatindex)
                {
                    updateThermostat(thermostat, channel);
                    return thermostat;
                }
            }
            UMRThermostat t = new UMRThermostat(this);
            t.thermostatindex = thermostatindex;
            t.thermostatType = umr2.umrConfig.ThermostatConfigs[thermostatindex].select;
            t.connectedChannels = new List<UMRChannel>();
            updateThermostat(t, channel);
            Thermostats.Add(t);
            return t;
        }

        private void RefreshStructs()
        {
            for (int i = 0; i < umr2.umrConfig.Channels.Count; i++)
            {
                if (umr2.umrConfig.OutputValveConfig[i].mode != "off")
                {
                    UMRChannel csO;
                    if (Channels.Count <= i)
                    {
                        csO = new UMRChannel(this);
                        Channels.Add(csO);
                    }
                    else
                    {
                        csO = Channels[i];
                    }
                    csO.valveindex = (ValveID)i;
                    csO.Thermostat = GetThermostat(umr2.umrConfig.Channels[i].label, csO);
                    csO.valveState = umr2.umrStatus.valves[i].state;
                }
            }

            if (this.umr2.umrStatus.heater.state == "on")
            {
                this.State = UMRState.UMR_HEATING;
                this.factor = this.umr2.umrStatus.heater.factor;
            }
            else if (this.umr2.umrStatus.cooler.state == "on")
            {
                this.State = UMRState.UMR_COOLING;
                this.factor = this.umr2.umrStatus.cooler.factor;
            }
            else
            {
                this.factor = 0;
                this.State = UMRState.UMR_IDLE;
            }
        }

        private bool DownloadConfig()
        {
            try
            {
                string inputThermostatsConfig = DownloadJson("settings.inputs.thermostats.*");
                JToken jsonThermostatConfig = JObject.Parse(inputThermostatsConfig)["settings"]["inputs"]["thermostats"];
                umr2.umrConfig.ThermostatConfigs = new List<ThermostatConfig>(jsonThermostatConfig.ToObject<ThermostatConfig[]>());

                string outputValveConfig = DownloadJson("settings.outputs.valves.*");
                JToken jsonValfConfig = JObject.Parse(outputValveConfig)["settings"]["outputs"]["valves"];
                umr2.umrConfig.OutputValveConfig = new List<Valf>(jsonValfConfig.ToObject<Valf[]>());

                string outputChannelConfig = DownloadJson("settings.channels.*");
                JToken jsonChannelConfig = JObject.Parse(outputChannelConfig)["settings"]["channels"];
                umr2.umrConfig.Channels = new List<Channel>(jsonChannelConfig.ToObject<Channel[]>());

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string DownloadJson(string jsonPartion)
        {
            try
            {
                return getclient.DownloadString("http://" + UMR2url + "/get.json?f=$." + jsonPartion);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return "";
            }
        }

        private bool UpdateUMRValues()
        {
            try
            {
                string outputs = DownloadJson("status.outputs.*");
                JToken jsonOutputStatus = JObject.Parse(outputs)["status"]["outputs"];
                umr2.umrStatus = jsonOutputStatus.ToObject<UMRStatusInternal>();

                string thermostats = DownloadJson("status.process.thermostats.*");
                JToken jsonArrayThermostats = JObject.Parse(thermostats)["status"]["process"]["thermostats"];
                umr2.umrStatus.Thermostats = new List<ThermostatInternal>(jsonArrayThermostats.ToObject<ThermostatInternal[]>());

                //Fanlink gives last seen status, currently not used
                //string fanlink = DownloadJson("status.communications.fanlink.*");
                //JToken jsonArraydatafanlink = JObject.Parse(fanlink)["status"]["communications"]["fanlink"]["devices"];
                //umr2.umrStatus.FanlinkDevices = new List<FanlinkDevice>(jsonArraydatafanlink.ToObject<FanlinkDevice[]>());

                RefreshStructs();
                return true;
            }
            catch(Exception e)
            {
                Console.WriteLine("Unable to download UMR data: " + e);
                return false;
            }
        }
    }
}
