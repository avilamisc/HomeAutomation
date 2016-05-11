using System;
using System.Threading.Tasks;
using Windows.Devices.I2c;
using Windows.Foundation;
using Windows.Devices.Enumeration;

namespace Guru.HomeAutomation.Sensors
{
    public sealed class HTU21D
    {
        //I2CBus Name for HTU21D Sensor
        private string i2cBus;

        /// <summary>
        /// Device addresses are constant and are uniquely identified by Microcontroller.
        /// Microcontroller and Sensors communicate over Serial Bus. See details below
        /// https://abhyast.wordpress.com/2016/03/30/under-hood-of-htu21df-sensor-part-1/
        /// 0x0040 is unique address for HTU21DF Sensor
        /// </summary>
        private const ushort HTU21DAddress = 0x0040;

        /// <summary>
        /// Below are commands to capture temperatuer and humidity with sensors.
        /// When Measuring Temperature / Humidity with Hold options, Microcontroller waits for measurement to complete before next measurement.
        /// For detailed description see below: https://abhyast.wordpress.com/2016/03/31/under-hood-of-htu21df-sensor-part-2-2/
        /// </summary>

        //Measure Temperature
        private const byte MeasureTemperatureWithHold = 0xE3;
        
        //Measure Humidity
        private const byte MeasureHumitityWithHold = 0xE5;


        /// <summary>
        /// Before Measuring Temperature / Humidity ensure device is properly initialized.
        /// </summary>
        private bool isDeviceAvailable = false;

        /// <summary>
        /// Abstraction for I2CDevice
        /// </summary>
        private I2cDevice i2cDevice;
        
        
        /// <summary>
        /// Constructor of HTU21D class
        /// </summary>
        /// <param name="i2cBusName"></param>
        public HTU21D(string i2cBusName)
        {
            this.i2cBus = i2cBusName;
        }

        /// <summary>
        /// Humdity property of HTU21D class is using by clients / consumers of this library.
        /// Public Humidity property is defined by private rawHumidity measured by sensor and 
        /// Calculation provided by sensor manufacturer.
        /// Refer to this blog for "Under the hood of HTU21D Sensor https://abhyast.wordpress.com/2016/04/01/under-hood-of-htu21df-sensor-part-2/
        /// </summary>
        public float Humidity
        {
            get
            {
                if (!this.isDeviceAvailable)
                {
                    return 0f;
                }

                ushort rawHumidityData = this.RawHumidity;
                double humidityRelative = ((125.0 * rawHumidityData) / 65536) - 6.0;

                return Convert.ToSingle(humidityRelative);
            }
        }



        /// <summary>
        /// Humdity property of HTU21D class is using by clients / consumers of this library.
        /// Public Temperature property is defined by private rawTemperature measured by sensor and 
        /// Calculation provided by sensor manufacturer.
        /// Refer to this blog for "Under the hood of HTU21D Sensor https://abhyast.wordpress.com/2016/04/01/under-hood-of-htu21df-sensor-part-2/
        /// </summary>
        public float Temperature
        {
            get
            {

                if (!this.isDeviceAvailable)
                {
                    return 0f;
                }

                ushort rawTemperatureData = this.RawTemperature;
                double temperatureCelsius = ((175.72 * rawTemperatureData) / 65536) - 46.85;

                return Convert.ToSingle(temperatureCelsius);
            }
        }



        public IAsyncOperation<bool> BeginAsync()
        {
            return this.AsyncHelper().AsAsyncOperation<bool>();
        }

        /// <summary>
        /// Initialzes HTU21D Sensor. 
        /// Measures Temperature and then validates it before returning condition of sensor (Initialized / Not Initialized)
        /// </summary>
        /// <returns></returns>
        private async Task<bool> AsyncHelper()
        {
            bool isDeviceInitalized = false;

            string advQuery = I2cDevice.GetDeviceSelector();
            DeviceInformationCollection dic = await DeviceInformation.FindAllAsync(advQuery);
            string deviceID = dic[0].Id;

            I2cConnectionSettings htu21dConnection = new I2cConnectionSettings(HTU21DAddress);
            htu21dConnection.BusSpeed = I2cBusSpeed.FastMode;
            htu21dConnection.SharingMode = I2cSharingMode.Shared;


            this.i2cDevice = await I2cDevice.FromIdAsync(deviceID, htu21dConnection);

            if (null == i2cDevice)
            {
                isDeviceInitalized = false;
            }
            else
            {
                byte[] temperature = new byte[3];

                try
                {
                    this.i2cDevice.WriteRead(new byte[] { HTU21D.MeasureTemperatureWithHold }, temperature);
                    isDeviceInitalized = true;
                }
                catch
                {
                    isDeviceInitalized = false;
                }
            }

            return isDeviceInitalized;
        }

        /// <summary>
        /// Caputures Raw Humidity from Sensor
        /// </summary>
        private ushort RawHumidity
        {
            get
            {
                ushort rawHumidity = 0;
                byte[] i2cHumidityData = new byte[3];
                this.i2cDevice.WriteRead(new byte[] { HTU21D.MeasureHumitityWithHold }, i2cHumidityData);

                ushort humidityData;
                ushort humidityMSB = i2cHumidityData[0];
                ushort humidityLSB = i2cHumidityData[1];
                ushort crcFromSensor = i2cHumidityData[2];

                humidityMSB = (ushort)(humidityMSB << 8);
                humidityLSB = (ushort)(humidityLSB & 0xfc);
                humidityData = (ushort)(humidityMSB | humidityLSB);
                bool isHumidityData, isValidData;

                if((i2cHumidityData[1] & 0x02) != 0x00)
                {
                    isHumidityData = false;
                }
                else
                {
                    isHumidityData = true;
                }

                if(isHumidityData)
                {
                    isValidData = this.ValidCRCCheck(humidityData, (byte)(crcFromSensor ^ 0x62));
                    if(isValidData)
                    {
                        rawHumidity = humidityData;
                    }
                    else
                    {
                        rawHumidity = 0;
                    }
                }
                else
                {
                    rawHumidity = 0;
                }


                return rawHumidity;


            }
        }


        /// <summary>
        /// Caputures Raw Temperature from Sensor
        /// </summary>
        private ushort RawTemperature
        {
            get
            {
                ushort rawTemperature = 0;
                byte[] i2cTemperatureData = new byte[3];
                this.i2cDevice.WriteRead(new byte[] { HTU21D.MeasureHumitityWithHold }, i2cTemperatureData);

                ushort temperatureData;
                ushort temperatureMSB = i2cTemperatureData[0];
                ushort temperatureLSB = i2cTemperatureData[1];
                ushort crcFromSensor = i2cTemperatureData[2];

                temperatureMSB = (ushort)(temperatureMSB << 8);
                temperatureLSB = (ushort)(temperatureLSB & 0xfc);
                temperatureData = (ushort)(temperatureMSB | temperatureLSB);

                bool isTemperatureData = false;
                bool isValidData = false;

                if ((i2cTemperatureData[1] & 0x02) != 0x00)
                {
                    isTemperatureData = false;
                }
                else
                {
                    isTemperatureData = true;
                }

                if (isTemperatureData)
                {
                    isValidData = this.ValidCRCCheck(temperatureData,(byte) crcFromSensor);
                    if (isValidData)
                    {
                        rawTemperature = temperatureData;
                    }
                    else
                    {
                        rawTemperature = 0;
                    }
                }
                else
                {
                    rawTemperature = 0;
                }


                
                return rawTemperature;
            }
        }

        /// <summary>
        /// Performs CRC check on data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="crc"></param>
        /// <returns></returns>
        private bool ValidCRCCheck(ushort data, byte crc)
        {
            bool isDataValid = false;

            const int crcBitLength = 8;

            const int dataLength = 16;

            const ushort GeneratorPolynomial = 0x0131;

            int crcData = data << crcBitLength;
            int currValue, bitPosition;
            for(int i = dataLength - 1; i >= 0;i--)
            {
                bitPosition = crcBitLength + i;
                currValue = crcData >> bitPosition;
                if((currValue & 0x01) == 0)
                {
                    continue;
                }
                else
                {
                    crcData ^= GeneratorPolynomial << i;
                }
            }
            if(crcData == crc)
            {
                isDataValid = true;
            }
            else
            {
                isDataValid = false;
            }
            return isDataValid;
        }


    }
}


   
