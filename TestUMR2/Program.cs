using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KluitNET.WTH.UMR2;

namespace KluitNET.WTH.UMR2.TEST
{
    class Program
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

        static void Main(string[] args)
        {
            double cachedTemperature = 0;

            // Create and init UMR controller comms class

            UMR2Controller.UMRThermostat fanlinkThermostat = null;
            UMR2Controller uMR2 = new UMR2Controller();

            uMR2.OnTemperatureChange += UMR2_OnTemperatureChange;
            uMR2.OnActiveChange += UMR2_OnActiveChange;
            uMR2.OnSetPointChange += UMR2_OnSetPointChange;
            uMR2.StartComms();

            // Adjust temperature test
            // It takes 0-2 minutes for the changed temperature to be represented in the program.

            Console.WriteLine("Press enter to set temperature for first fanlink device");
            Console.ReadLine();
            foreach(var thermostat in uMR2.Thermostats)
            {
                if(thermostat.isFanlinkThermostat)
                {
                    fanlinkThermostat = thermostat;
                    cachedTemperature = fanlinkThermostat.Setpoint;
                    fanlinkThermostat.SetPointTemperature(fanlinkThermostat.Setpoint + 1);
                    Console.WriteLine("It might take some time until the changed temperature is replied from the UMR... Please be patient");
                    break;
                }
            }
            if (fanlinkThermostat != null)
            {
                Console.WriteLine("Press enter to restore set temperature for first fanlink device");
                Console.ReadLine();
                fanlinkThermostat.SetPointTemperature(cachedTemperature);
            }
            else
            {
                Console.WriteLine("No fanlink device found");
            }

            Console.WriteLine("Press enter to close");
            Console.ReadLine();
        }

        private static void UMR2_OnSetPointChange(UMR2Controller caller, UMR2Controller.UMRThermostat thermostat)
        {
            Console.WriteLine(thermostat.ToString() + "\tnew setpoint\t" + thermostat.Setpoint);
        }

        private static void UMR2_OnActiveChange(UMR2Controller caller, UMR2Controller.UMRThermostat thermostat)
        {
            Console.WriteLine(thermostat.ToString() + "\tactive changed\t" + thermostat.IsActive);
        }

        private static void UMR2_OnTemperatureChange(UMR2Controller caller, UMR2Controller.UMRThermostat thermostat)
        {
            Console.WriteLine(thermostat.ToString() + "\tnew temperature\t" + thermostat.Temperature);
        }
    }
}
