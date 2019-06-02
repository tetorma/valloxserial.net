using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Markup.Localizer;

namespace ValloxSerialNet
{
    /// <summary>
    /// Static functions and definitions of the vallox protocol
    /// 
    /// 01 21 11 00 A3 C9
    /// |  |  |  |  |  |
    /// |  |  |  |  |  CRC
    /// |  |  |  |  Get = Variable, Set = value
    /// |  |  |  Get = 0, >1 = variable
    /// |  |  Receiver
    /// |  Sender 11 = mainboard 21.. control panels 10 boroadcast to mainboards, 20 broadcast to panels
    /// Domain always 1
    /// 
    /// Special telegram (two times) for suspending the bus as the CO2 sensor communication follows another protocol.
    /// 01 11 20 91 0 C3   (91 = SUSPEND)
    /// 01 11 20 9F 0 C3   (8F = RESUME) 
    /// 
    /// Master is always 11
    /// Panels 21-29
    /// LON is always 28
    /// 
    /// RH% sensor value: (x-51)/2.04
    /// 
    /// Näherung im "linearen" Bereich 0x73: Temperatur: 0x73 = dez 115 --> Temp = (115-100)/3 = 5 Grad Celsius 
    /// </summary>
    public class Vallox
    {
        #region VALLOX_PROTOCOL

        public const int TelegramLength = 6; // always 6
        public const int Domain = 1; // always 1

        /// <summary>
        /// variable 0 is reserved for polling.
        /// </summary>
        public const byte PollRequest = 0x00;


        /// <summary>
        /// contains all information about a variable.
        /// </summary>
        public static Dictionary<Vallox.Variable.VariableNumber, string> VariableNames = new Dictionary<Vallox.Variable.VariableNumber, string>()
        {
            {(Vallox.Variable.VariableNumber)0x06, "IoPort FanSpeed-Relays"},
            {(Vallox.Variable.VariableNumber)0x07, "IoPort MultiPurpose 1"},
            {(Vallox.Variable.VariableNumber)0x08, "IoPort MultiPurpose 2"},

            {(Vallox.Variable.VariableNumber)0x29, "Fan Speed"},
            {(Vallox.Variable.VariableNumber)0x2A, "Humidity"},
            {(Vallox.Variable.VariableNumber)0x2B, "CO2 high"},
            {(Vallox.Variable.VariableNumber)0x2C, "CO2 low"},
            {(Vallox.Variable.VariableNumber)0x2D, "Machine Installed C02Sensors"},
            {(Vallox.Variable.VariableNumber)0x2E, "Current/Voltage in mA incomming on machine"},

            {(Vallox.Variable.VariableNumber)0x2F, "Humidity sensor 1 raw: (x-51)/2.04"},
            {(Vallox.Variable.VariableNumber)0x30, "Humidity sensor 2 raw: (x-51)/2.04"},

            {(Vallox.Variable.VariableNumber)0x32, "Temperature outside raw"},
            {(Vallox.Variable.VariableNumber)0x33, "Temperature exhaust raw"},
            {(Vallox.Variable.VariableNumber)0x34, "Temperature inside raw"},
            {(Vallox.Variable.VariableNumber)0x35, "Temperature incomming raw"},
            {(Vallox.Variable.VariableNumber)0x36, "Last error number"},

            {(Vallox.Variable.VariableNumber)0x55, "Post heating on counter"},
            {(Vallox.Variable.VariableNumber)0x56, "Post heating off time"},
            {(Vallox.Variable.VariableNumber)0x57, "Post heating target value"},

            {(Vallox.Variable.VariableNumber)0x6C, "Flags 1"},
            {(Vallox.Variable.VariableNumber)0x6D, "Flags 2"},
            {(Vallox.Variable.VariableNumber)0x6E, "Flags 3"},
            {(Vallox.Variable.VariableNumber)0x6F, "Flags 4"},
            {(Vallox.Variable.VariableNumber)0x70, "Flags 5"},
            {(Vallox.Variable.VariableNumber)0x71, "Flags 6"},

            {(Vallox.Variable.VariableNumber)0x79, "Fireplace booster count down minutes"},
           
            {(Vallox.Variable.VariableNumber)0x8F, "Resume bus"},
            {(Vallox.Variable.VariableNumber)0x91, "Suspend bus for CO2 communication"},
           
            {(Vallox.Variable.VariableNumber)0xA3, "Select"},
            {(Vallox.Variable.VariableNumber)0xA4, "Heating set point"},
            {(Vallox.Variable.VariableNumber)0xA5, "Fan Speed max"},
            {(Vallox.Variable.VariableNumber)0xA6, "Service reminder months"},
            {(Vallox.Variable.VariableNumber)0xA7, "Preheating set point"},
            {(Vallox.Variable.VariableNumber)0xA8, "Input fan stop temperature threshold"},
            {(Vallox.Variable.VariableNumber)0xA9, "Fan Speed min"},
            {(Vallox.Variable.VariableNumber)0xAA, "Program"},
            {(Vallox.Variable.VariableNumber)0xAB, "Maintencance countdown months"},
           
            {(Vallox.Variable.VariableNumber)0xAE, "Basic humidity level"},
            {(Vallox.Variable.VariableNumber)0xAF, "Heat recovery cell bypass setpoint temperature"},
            {(Vallox.Variable.VariableNumber)0xB0, "DC fan input adjustment %"},
            {(Vallox.Variable.VariableNumber)0xB1, "DC fan output adjustment %"},
            {(Vallox.Variable.VariableNumber)0xB2, "Cell defrosting setpoint temperature"},
            {(Vallox.Variable.VariableNumber)0xB3, "CO2 set point upper"},
            {(Vallox.Variable.VariableNumber)0xB4, "CO2 set point lower"},
            {(Vallox.Variable.VariableNumber)0xB5, "Program 2"},
        };

        public class Adress
        {
            public const int Mainboards = 0x10;
            public const int Panels = 0x20;

            // Addresses for sender and receiver
            public const int Master = Mainboards + 1;


            public const int Panel1 = Panels + 1;
            public const int Panel2 = Panels + 2;
            public const int Panel3 = Panels + 3;
            public const int Panel4 = Panels + 4;
            public const int Panel5 = Panels + 5;
            public const int Panel6 = Panels + 6;
            public const int Panel7 = Panels + 7;
            public const int Lon = Panels + 8;
            public const int Panel8 = Panels + 9;
        }

        public class Variable
        {
            public enum VariableNumber : Byte
            {
            // 1 1 1 1 1 1 1 1  
            // | | | | | | | |
            // | | | | | | | +- 0 Speed 1 - 0=0ff 1=on - readonly
            // | | | | | | +--- 1 Speed 2 - 0=0ff 1=on - readonly
            // | | | | | +----- 2 Speed 3 - 0=0ff 1=on - readonly
            // | | | | +------- 3 Speed 4 - 0=0ff 1=on - readonly
            // | | | +--------- 4 Speed 5 - 0=0ff 1=on - readonly
            // | | +----------- 5 Speed 6 - 0=0ff 1=on - readonly
            // | +------------- 6 Speed 7 - 0=0ff 1=on - readonly
            // +--------------- 7 Speed 8 - 0=0ff 1=on - readonly
            /*public const int*/ IoPortFanSpeedRelays = 0x06,

            // 1 1 1 1 1 1 1 1 
            // | | | | | | | |
            // | | | | | | | +- 0 
            // | | | | | | +--- 1 
            // | | | | | +----- 2 
            // | | | | +------- 3 
            // | | | +--------- 4 
            // | | +----------- 5  post-heating on - 0=0ff 1=on - readonly
            // | +------------- 6 
            // +--------------- 7 
            /*public const int*/ IoPortMultiPurpose1 = 0x07,

            // 1 1 1 1 1 1 1 1  
            // | | | | | | | |
            // | | | | | | | +- 0 
            // | | | | | | +--- 1 damper motor position - 0=winter 1=season - readonly
            // | | | | | +----- 2 fault signal relay - 0=open 1=closed - readonly
            // | | | | +------- 3 supply fan - 0=on 1=off
            // | | | +--------- 4 pre-heating - 0=off 1=on - readonly
            // | | +----------- 5 exhaust-fan - 0=on 1=off
            // | +------------- 6 fireplace-booster - 0=open 1=closed - readonly 
            // +--------------- 7 
            /*public const int*/ IoPortMultiPurpose2 = 0x08,

            // see FanSpeedMapping
            //01H = speed 1
            //03H = speed 2
            //07H = speed 3
            //0FH = speed 4
            //1FH = speed 5
            //3FH = speed 6
            //7FH = speed 7
            //FFH = speed 8
            /*public const int*/ FanSpeed = 0x29,

            // Variables 
            // 33H = 0% FFH = 100%
            /*public const int*/ Humidity = 0x2A, // higher measured relative humidity from 2F and 30. Translating Formula (x-51)/2.04
            /*public const int*/ Co2High = 0x2B,
            /*public const int*/ Co2Low = 0x2C,

            // 1 1 1 1 1 1 1 1 
            // | | | | | | | |
            // | | | | | | | +- 0 
            // | | | | | | +--- 1 Sensor1 - 0=not installed 1=installed - readonly
            // | | | | | +----- 2 Sensor2 - 0=not installed 1=installed - readonly
            // | | | | +------- 3 Sensor3 - 0=not installed 1=installed - readonly
            // | | | +--------- 4 Sensor4 - 0=not installed 1=installed - readonly
            // | | +----------- 5 Sensor5 - 0=not installed 1=installed - readonly
            // | +------------- 6 
            // +--------------- 7 
            /*public const int*/ MachineInstalledC02Sensor = 0x2D,

            /*public const int*/ CurrentIncomming = 0x2E, // Current/Voltage in mA incomming on machine - readonly

            /*public const int*/ HumiditySensor1 = 0x2F, // sensor value: (x-51)/2.04
            /*public const int*/ HumiditySensor2 = 0x30, // sensor value: (x-51)/2.04

            /*public const int*/ TempOutside = 0x32,
            /*public const int*/ TempExhaust = 0x33,
            /*public const int*/ TempInside = 0x34,
            /*public const int*/ TempIncomming = 0x35,


            //05H = Supply air temperature sensor fault
            //06H = Carbon dioxide alarm
            //07h = Outdoor air sensor fault
            //08H = Extract air sensor fault
            //09h = Water radiator danger of freezing
            //0AH = Exhaust air sensor fault
            /*public const int*/ LastErrorNumber = 0x36,

            //Post-heating power-on seconds counter. Percentage of X / 2.5
            /*public const int*/ PostHeatingOnCounter = 0x55,

            //Post-heating off time, in seconds, the counter. Percentage of X / 2.5
            /*public const int*/ PostHeatingOffTime = 0x56,

            //The ventilation zone of air blown to the desired temperature NTC sensor scale
            /*public const int*/ PostHeatingTargetValue = 0x57,

            // 1 1 1 1 1 1 1 1 
            // | | | | | | | |
            // | | | | | | | +- 0 
            // | | | | | | +--- 1 
            // | | | | | +----- 2 
            // | | | | +------- 3 
            // | | | +--------- 4 
            // | | +----------- 5 
            // | +------------- 6 
            // +--------------- 7 
            /*public const int*/ Flags1 = 0x6C,

            // 1 1 1 1 1 1 1 1 
            // | | | | | | | |
            // | | | | | | | +- 0 CO2 higher speed-request 0=no 1=Speed​​. up
            // | | | | | | +--- 1 CO2 lower rate public invitation 0=no 1=Speed​​. down
            // | | | | | +----- 2 %RH lower rate public invitation 0=no 1=Speed​​. down
            // | | | | +------- 3 switch low. Spd.-request 0=no 1=Speed ​. down
            // | | | +--------- 4 
            // | | +----------- 5 
            // | +------------- 6 CO2 alarm 0=no 1=CO2 alarm
            // +--------------- 7 sensor Frost alarm 0=no 1=a risk of freezing
            /*public const int*/ Flags2 = 0x6D,

            // 1 1 1 1 1 1 1 1 
            // | | | | | | | |
            // | | | | | | | +- 0 
            // | | | | | | +--- 1 
            // | | | | | +----- 2 
            // | | | | +------- 3 
            // | | | +--------- 4 
            // | | +----------- 5 
            // | +------------- 6 
            // +--------------- 7 
            /*public const int*/ Flags3 = 0x6E,

            // 1 1 1 1 1 1 1 1 
            // | | | | | | | |
            // | | | | | | | +- 0 
            // | | | | | | +--- 1 
            // | | | | | +----- 2 
            // | | | | +------- 3 
            // | | | +--------- 4 water radiator danger of freezing 0=no risk 1 = risk
            // | | +----------- 5 
            // | +------------- 6 
            // +--------------- 7 slave/master selection 0=slave 1=master
            /*public const int*/ Flags4 = 0x6F,

            // 1 1 1 1 1 1 1 1 
            // | | | | | | | |
            // | | | | | | | +- 0 
            // | | | | | | +--- 1 
            // | | | | | +----- 2 
            // | | | | +------- 3 
            // | | | +--------- 4 
            // | | +----------- 5 
            // | +------------- 6 
            // +--------------- 7 preheating status flag 0=on 1=off
            /*public const int*/ Flags5 = 0x70,

            // 1 1 1 1 1 1 1 1 
            // | | | | | | | |
            // | | | | | | | +- 0 
            // | | | | | | +--- 1 
            // | | | | | +----- 2 
            // | | | | +------- 3 
            // | | | +--------- 4 remote monitoring control 0=no 1=Operation - readonly
            // | | +----------- 5 Activation of the fireplace switch read the variable and set this number one
            // | +------------- 6 fireplace/booster status 0=off 1=on - read only
            // +--------------- 7
            /*public const int*/ Flags6 = 0x71,

            //Function time in minutes remaining , descending - readonly
            /*public const int*/ FirePlaceBoosterCounter = 0x79,

            // Suspend Resume Traffic for CO2 sensor interaction: is sent twice as broadcast
            /*public const int*/ SuspendBus = 0x91,
            /*public const int*/ ResumeBus = 0x8F,

            // 1 1 1 1 1 1 1 1
            // | | | | | | | |
            // | | | | | | | +- 0 Power state
            // | | | | | | +--- 1 CO2 Adjust state
            // | | | | | +----- 2 %RH adjust state
            // | | | | +------- 3 Heating state
            // | | | +--------- 4 Filterguard indicator - readonly
            // | | +----------- 5 Heating indicator - readonly
            // | +------------- 6 Fault indicator - readonly
            // +--------------- 7 Service reminder - readonly
            /*public const int*/ Select = 0xA3,

            /*public const int*/ HeatingSetPoint = 0xA4,

            //01H = Speed 1
            //03H = Speed 2
            //07H = Speed 3
            //0FH = Speed 4
            //1FH = Speed 5
            //3FH = Speed 6
            //7FH = Speed 7
            //FFH = Speed 8
            /*public const int*/ FanSpeedMax = 0xA5,

            /*public const int*/ ServiceReminder = 0xA6, // months
            /*public const int*/ PreHeatingSetPoint = 0xA7,

            /*public const int*/ InputFanStop = 0xA8,  // Temp threshold: fan stops if input temp falls below this temp.

            //01H = Speed 1
            //03H = Speed 2
            //07H = Speed 3
            //0FH = Speed 4
            //1FH = Speed 5
            //3FH = Speed 6
            //7FH = Speed 7
            //FFH = Speed 8
            /*public const int*/ FanSpeedMin = 0xA9,

            // 1 1 1 1 1 1 1 1
            // | | | | _______
            // | | | |     |  
            // | | | |     +--- 0-3 set adjustment interval of CO2 and %RH in minutes 
            // | | | |   
            // | | | |   
            // | | | | 
            // | | | +--------- 4 automatic RH basic level seeker state
            // | | +----------- 5 boost switch modde (1=boost, 0 = fireplace)
            // | +------------- 6 radiator type 0 = electric, 1 = water
            // +--------------- 7 cascade adjust 0 = off, 1 = on
            /*public const int*/ Program = 0xAA,

            //The maintenance counter month Inform the next maintenance alarm time remaining months. Descending.
            /*public const int*/ MaintenanceMonthCounter = 0xAB,

            /*public const int*/ BasicHumidityLevel = 0xAE,
            /*public const int*/ HrcBypass = 0xAF, // Heat recovery cell bypass setpoint temp
            /*public const int*/ DcFanInputAdjustment = 0xB0, // %
            /*public const int*/ DcFanOutputAdjustment = 0xB1, // %

            /*public const int*/ CellDefrosting = 0xB2, // Defrosting starts when exhaust air drops below this setpoint temp 

            /*public const int*/ Co2SetPointUpper = 0xB3,
            /*public const int*/ Co2SetPointLower = 0xB4,


            // 1 1 1 1 1 1 1 1
            // | | | | | | | |
            // | | | | | | | +- 0 Function of max speed limit 0 = with adjustment, 1 = always 
            // | | | | | | +--- 1  
            // | | | | | +----- 2
            // | | | | +------- 3
            // | | | +--------- 4
            // | | +----------- 5
            // | +------------- 6
            // +--------------- 7
            /*public const int*/ Program2 = 0xB5,

            // This one is queried at startup and answered with 3 but not described in the protocol: version?
            /*public const int*/ Unknown = 0xC0,
        };
        }

       

        // VALLOX_VARIABLE_PROGRAM2
        public enum MaxSpeedLimitMode
        {
            WithAdjustment,
            Always
        }

        // VALLOX_VARIABLE_PROGRAM
        public enum BoostSwitchMode
        {
            Boost,
            Fireplace
        }

        // VALLOX_VARIABLE_PROGRAM
        public enum RadiatorType
        {
            Electric,
            Water
        }

        public static Byte[] FanSpeedMapping =
        {
            0x01,
            0x03,
            0x07,
            0x0F,
            0x1F,
            0x3F,
            0x7F,
            0xFF
        };

        public static int[] TemperatureMapping =
        {
            -74,-70,-66,-62,-59,-56,-54,-52, 
            -50,-48,-47,-46,-44,-43,-42,-41,
            -40,-39,-38,-37,-36,-35,-34,-33,
            -33,-32,-31,-30,-30,-29,-28,-28,
            -27,-27,-26,-25,-25,-24,-24,-23,
            -23,-22,-22,-21,-21,-20,-20,-19,
            -19,-19,-18,-18,-17,-17,-16,-16,
            -16,-15,-15,-14,-14,-14,-13,-13,
            -12,-12,-12,-11,-11,-11,-10,-10,
            -09,-09,-09,-08,-08,-08,-07,-07,
            -07,-06,-06,-06,-05,-05,-05,-04,
            -04,-04,-03,-03,-03,-02,-02,-02,
            -01,-01,-01,-01, 00, 00, 00, 01, 
             01, 01, 02, 02, 02, 03, 03, 03,
             04, 04, 04, 05, 05, 05, 05, 06,
             06, 06, 07, 07, 07, 08, 08, 08,
             09, 09, 09, 10, 10, 10, 11, 11,
             11, 12, 12, 12 ,13, 13, 13, 14,
             14, 14, 15, 15, 15, 16, 16, 16,
             17, 17, 18, 18, 18, 19, 19, 19, 
             20, 20, 21, 21, 21, 22, 22, 22,
             23, 23, 24, 24, 24, 25, 25, 26,
             26, 27, 27, 27, 28, 28, 29, 29, 
             30, 30, 31, 31, 32, 32, 33, 33, 
             34, 34, 35, 35 ,36, 36, 37, 37, 
             38, 38, 39, 40, 40, 41, 41, 42,
             43, 43, 44, 45, 45, 46, 47, 48,
             49, 49, 50, 51, 52, 53, 53, 54, 
             55, 56, 57, 59, 60, 61, 62, 63,
             65, 66, 68, 69, 71, 73, 75, 77, 
             79, 81, 82, 86, 90, 93, 97, 100,
             100, 100, 100, 100, 100, 100, 100, 100
        };

        #endregion

        #region Statics

        // 1 1 1 1 1 1 1 1
        // | | | | | | | |
        // | | | | | | | +- 0 Power state
        // | | | | | | +--- 1 CO2 Adjust state
        // | | | | | +----- 2 %RH adjust state
        // | | | | +------- 3 Heating state
        // | | | +--------- 4 Filterguard indicator
        // | | +----------- 5 Heating indicator
        // | +------------- 6 Fault indicator
        // +--------------- 7 service reminder
        public static void ConvertSelect(Byte select, out bool powerState, out bool co2AdjustState, out bool humidityAdjustState, out bool heatingState, out bool filterGuardIndicator, out bool heatingIndicator, out bool faultIndicator, out bool serviceReminderIndicator)
        {
            powerState = (select & 0x01) != 0;
            co2AdjustState = (select & 0x02) != 0;
            humidityAdjustState = (select & 0x04) != 0;
            heatingState = (select & 0x08) != 0;
            filterGuardIndicator = (select & 0x10) != 0;
            heatingIndicator = (select & 0x20) != 0;
            faultIndicator = (select & 0x40) != 0;
            serviceReminderIndicator = (select & 0x80) != 0;
        }

        // 1 1 1 1 1 1 1 1
        // | | | | _______
        // | | | |     |  
        // | | | |     +--- 0-3 set adjustment interval of CO2 and %RH in minutes 
        // | | | |   
        // | | | |   
        // | | | | 
        // | | | +--------- 4 automatic RH basic level seeker state
        // | | +----------- 5 boost switch modde (1=boost, 0 = fireplace)
        // | +------------- 6 radiator type 0 = electric, 1 = water
        // +--------------- 7 cascade adjust 0 = off, 1 = on
        public static void ConvertProgram(Byte program, out int adjustmentIntervalMinutes, out bool automaticHumidityLevelSeekerState, out BoostSwitchMode boostSwitchMode, out RadiatorType radiatorType, out bool cascadeAdjust)
        {
            adjustmentIntervalMinutes = program & 0x0F;

            automaticHumidityLevelSeekerState = (program & 0x10) != 0;
            if ((program & 0x20) == 0)
            {
                boostSwitchMode = BoostSwitchMode.Fireplace;
            }
            else
            {
                boostSwitchMode = BoostSwitchMode.Boost;
            }

            if ((program & 0x40) == 0)
            {
                radiatorType = RadiatorType.Electric;
            }
            else
            {
                radiatorType = RadiatorType.Water;
            }

            cascadeAdjust = (program & 0x80) != 0;
        }

        // 1 1 1 1 1 1 1 1
        // | | | | | | | |
        // | | | | | | | +- 0 Function of max speed limit 0 = with adjustment, 1 = always 
        // | | | | | | +--- 1  
        // | | | | | +----- 2
        // | | | | +------- 3
        // | | | +--------- 4
        // | | +----------- 5
        // | +------------- 6
        // +--------------- 7
        public static void ConvertProgram2(Byte program2, out MaxSpeedLimitMode maxSpeeLimitMode)
        {
            if ((program2 & 0x01) == 0)
            {
                maxSpeeLimitMode = MaxSpeedLimitMode.WithAdjustment;
            }
            else
            {
                maxSpeeLimitMode = MaxSpeedLimitMode.WithAdjustment;
            }
        }

        // to readable string
        public static string ConvertAddress(Byte sender)
        {
            string address = string.Format("{0}", sender);
            switch (sender)
            {
                case Adress.Mainboards:
                    address = "Mainboards";
                    break;

                case Adress.Master:
                    address = "Master";
                    break;

                case Adress.Panels:
                    address = "Panels";
                    break;

                case Adress.Panel1:
                    address = "Panel1";
                    break;

                case Adress.Panel2:
                    address = "Panel2";
                    break;

                case Adress.Panel3:
                    address = "Panel3";
                    break;

                case Adress.Panel4:
                    address = "Panel4";
                    break;

                case Adress.Panel5:
                    address = "Panel5";
                    break;

                case Adress.Panel6:
                    address = "Panel6";
                    break;

                case Adress.Panel7:
                    address = "Panel7";
                    break;

                case Adress.Lon:
                    address = "LON";
                    break;

                case Adress.Panel8:
                    address = "Panel8";
                    break;
            }

            return address;
        }

        // to readable string
        public static string ConvertVariable(Variable.VariableNumber command)
        {
            string variable = string.Format("{0:X02}", (Byte)command);
            switch (command)
            {
                case Variable.VariableNumber.FanSpeed:
                    {
                        variable = "Fan speed";
                        break;
                    }

                case Variable.VariableNumber.Humidity:
                    {
                        variable = "Humidity";
                        break;
                    }

                case Variable.VariableNumber.Co2High:
                    {
                        variable = "CO2 high";
                        break;
                    }
                case Variable.VariableNumber.Co2Low:
                    {
                        variable = "CO2 low";
                        break;
                    }

                case Variable.VariableNumber.HumiditySensor1:
                    {
                        variable = "Humidity sensor 1";
                        break;
                    }
                case Variable.VariableNumber.HumiditySensor2:
                    {
                        variable = "Humidity sensor 2";
                        break;
                    }

                case Variable.VariableNumber.TempOutside:
                    {
                        variable = "Temp outside";
                        break;
                    }
                case Variable.VariableNumber.TempExhaust:
                    {
                        variable = "Temp exhaust";
                        break;
                    }
                case Variable.VariableNumber.TempInside:
                    {
                        variable = "Temp inside";
                        break;
                    }
                case Variable.VariableNumber.TempIncomming:
                    {
                        variable = "Temp incomming";
                        break;
                    }
                case Variable.VariableNumber.Select:
                    {
                        variable = "Select";
                        break;
                    }
                case Variable.VariableNumber.HeatingSetPoint:
                    {
                        variable = "Heating set point";
                        break;
                    }
                case Variable.VariableNumber.FanSpeedMax:
                    {
                        variable = "Fan speed max";
                        break;
                    }
                case Variable.VariableNumber.ServiceReminder:
                    {
                        variable = "Service reminder";
                        break;
                    }
                case Variable.VariableNumber.PreHeatingSetPoint:
                    {
                        variable = "Pre heating set point";
                        break;
                    }
                case Variable.VariableNumber.InputFanStop:
                    {
                        variable = "Input fan speed stop temp";
                        break;
                    }
                case Variable.VariableNumber.FanSpeedMin:
                    {
                        variable = "Fan speed min";
                        break;
                    }
                case Variable.VariableNumber.Program:
                    {
                        variable = "Program";
                        break;
                    }
                case Variable.VariableNumber.BasicHumidityLevel:
                    {
                        variable = "Basic humidity level";
                        break;
                    }
                case Variable.VariableNumber.HrcBypass:
                    {
                        variable = "HRC bypass";
                        break;
                    }
                case Variable.VariableNumber.DcFanInputAdjustment:
                    {
                        variable = "DC fan input adjustment";
                        break;
                    }
                case Variable.VariableNumber.DcFanOutputAdjustment:
                    {
                        variable = "DC fan output adjustment";
                        break;
                    }
                case Variable.VariableNumber.CellDefrosting:
                    {
                        variable = "Cell defrosting";
                        break;
                    }
                case Variable.VariableNumber.Co2SetPointUpper:
                    {
                        variable = "CO2 set point upper";
                        break;
                    }
                case Variable.VariableNumber.Co2SetPointLower:
                    {
                        variable = "CO2 set point lower";
                        break;
                    }
                case Variable.VariableNumber.Program2:
                    {
                        variable = "Program2";
                        break;
                    }
                case Variable.VariableNumber.Flags1:
                    {
                        variable = "Flags1";
                        break;
                    }
                case Variable.VariableNumber.Flags2:
                    {
                        variable = "Flags2";
                        break;
                    }
                case Variable.VariableNumber.Flags3:
                    {
                        variable = "Flags3";
                        break;
                    }
                case Variable.VariableNumber.Flags4:
                    {
                        variable = "Flags4";
                        break;
                    }
                case Variable.VariableNumber.Flags5:
                    {
                        variable = "Flags5";
                        break;
                    }
                case Variable.VariableNumber.Flags6:
                    {
                        variable = "Flags6";
                        break;
                    }
                case Variable.VariableNumber.IoPortFanSpeedRelays:
                    {
                        variable = "IoPortFanSpeedRelays";
                        break;
                    }
                case Variable.VariableNumber.IoPortMultiPurpose1:
                    {
                        variable = "IoPortMultiPurpose1";
                        break;
                    }
                case Variable.VariableNumber.IoPortMultiPurpose2:
                    {
                        variable = "IoPortMultiPurpose2";
                        break;
                    }
                case Variable.VariableNumber.MachineInstalledC02Sensor:
                    {
                        variable = "MachineInstalledC02Sensor";
                        break;
                    }
                case Variable.VariableNumber.PostHeatingOnCounter:
                    {
                        variable = "PostHeatingOnCounter";
                        break;
                    }
                case Variable.VariableNumber.PostHeatingOffTime:
                    {
                        variable = "PostHeatingOffTime";
                        break;
                    }
                case Variable.VariableNumber.PostHeatingTargetValue:
                    {
                        variable = "PostHeatingTargetValue";
                        break;
                    }
                case Variable.VariableNumber.FirePlaceBoosterCounter:
                    {
                        variable = "FirePlaceBoosterCounter";
                        break;
                    }
                case Variable.VariableNumber.MaintenanceMonthCounter:
                    {
                        variable = "MaintenanceMonthCounter";
                        break;
                    }
                case Variable.VariableNumber.LastErrorNumber:
                    {
                        variable = "LastErrorNumber";
                        break;
                    }
                case Variable.VariableNumber.Unknown:
                    {
                        variable = "Unknown";
                        break;
                    }
            }

            return variable;
        }

        public static int ConvertTemp(Byte value)
        {
            return TemperatureMapping[value];
        }

        // 0xFF --> 8
        public static int ConvertFanSpeed(Byte value)
        {
            int fanSpeed = 0;

            for (int i = 0; i < 8; i++)
            {
                if (FanSpeedMapping[i] == value)
                {
                    fanSpeed = i+1;
                    break;
                }
            }

            return fanSpeed;
        }

        // 8 --> 0xFF
        public static Byte ConvertBackFanSpeed(int value)
        {
            Byte fanSpeed = FanSpeedMapping[value];
            return fanSpeed;
        }

        public static byte ComputeCheckSum(params byte[] bytes)
        {
            int checksum = 0;
            if (bytes != null && bytes.Length > 0)
            {
                foreach (byte b in bytes)
                {
                    checksum += b;
                }
            }
            return (byte)(checksum % 256);
        }

        // Domain is always 1 with checksum
        public static Byte[] CreateTelegram(Byte sender, Byte receiver, Vallox.Variable.VariableNumber variable, Byte value)
        {
            Byte[] telegram = new Byte[Vallox.TelegramLength] { Vallox.Domain, sender, receiver, (Byte)variable, value, 0x00 };
            telegram[Vallox.TelegramLength - 1] = ComputeCheckSum(telegram);
            return telegram;
        }

        #endregion
    }
}
