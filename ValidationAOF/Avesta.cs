using IvanMath;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using System.Xml;
using static Spectrometer.UsbCCD;

namespace Spectrometer
{
    public class Avesta : Spectrometer
    {
        private TCCDUSBParams parameters = new TCCDUSBParams();
        private TCCDUSBExtendParams extendedParameters = new TCCDUSBExtendParams();

        public string configFile = "";
        public string sensivityFile = "";

        private int averagesCount = 1;

        public List<DataPoint> sensititvityList = new List<DataPoint>(); // массив чувствительности

        public TCCDUSBExtendParams ExtendParameters
        {
            get { return extendedParameters; }
        }

        public TCCDUSBParams Parameters
        {
            get { return parameters; }
        }

        private int id = 0;
        public int ID
        {
            get { return id; }
        }

        public Timer timer = new Timer();

        public override SpectrometerType Type => SpectrometerType.Avesta;

        private Avesta(int id)
        {
            int _id = id;
            bool connected = CCD_Init(IntPtr.Zero, "", ref _id);
            if (connected)
            {
                bool res = CCD_GetParameters(id, ref parameters);
                res = CCD_GetExtendParameters(id, ref extendedParameters);
            }
            else
            {
                throw new Exception("No connected devices");
            }
        }

        public static Avesta Connect(int deviceID)
        {
            Avesta av = new Avesta(deviceID);
            return av;
        }

        private int[] GetData(bool correctSensetivity = true)
        {
            int pixels = extendedParameters.nNumPixelsH * 1;//ExtendParams.nNumPixelsH * ExtendParams.nNumPixelsV * ExtendParams.nNumReadOuts;
            int cb = pixels * sizeof(uint);
            IntPtr pData = Marshal.AllocHGlobal(cb);

            bool res = UsbCCD.CCD_InitMeasuring(0);
            UsbCCD.CCD_StartWaitMeasuring(0);

            int[] data = new int[pixels];
            byte[] bData = new byte[cb];
            UsbCCD.CCD_GetData(0, pData);

            Marshal.Copy(pData, data, 0, data.Length);
            Marshal.FreeHGlobal(pData);

            if(correctSensetivity)
                for(int i = 0; i < data.Length; i++)
                {
                    data[i] = (int)(data[i] / sensitivitys[i]);
                }

            return data;
        }

        public override List<DataPoint> GetSpectrum()
        {
            int[][] data = new int[averagesCount][];
            List<DataPoint> spectrum = new List<DataPoint>();

            for (int i = 0; i < averagesCount; i++)
            {
                data[i] = GetData(true);
            }

            double avr = 0;

            if (data.Length > 0)
            {
                for (int i = 0; i < data[0].Length; i++)
                {
                    for (int j = 0; j < data.Length; j++)
                    {
                        avr += data[j][i];
                    }
                    spectrum.Add(new DataPoint( Index2Wavelength(i), avr/data.Length) );
                    avr = 0;
                }
            }
            else
                throw new Exception("N should be >= 1");

            spectrum.Sort(PointComparer);
            return spectrum;
        }

        public static int PointComparer(DataPoint p1, DataPoint p2)
        {
            if (p1.X > p2.X)
                return 1;
            if (p1.X < p2.X)
                return -1;
            return 0;
        }

        /*private void CaptureAveragedSpectralCurve(in int N, out double[] avrData, bool correctSensivity = true)
        {
            int[][] data = new int[N][];

            for (int i = 0; i < N; i++)
            {
                data[i] = GetData(correctSensivity);
            }

            avrData = new double[data[0].Length];

            if (data.Length > 0)
            {
                for (int i = 0; i < data[0].Length; i++)
                {
                    for (int j = 0; j < data.Length; j++)
                    {
                        avrData[i] += data[j][i];
                    }
                    avrData[i] /= data.Length;
                }
            }
            else
                throw new Exception("N should be >= 1");
        }*/

        /*public static List<int> EnumerateAvestaDevicesIDs()
        {
            List<int> devices = new List<int>();
            bool flag = false;
            if (flag = CCD_Init(0, null, ))
                devices.Add(0);
            if (CCD_HitTest(1))
                devices.Add(1);
            if (CCD_HitTest(2))
                devices.Add(2);
            return devices;
        }*/

        public void LoadConfigFromFile(string configPath)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(configPath);
            XmlElement configs = xmlDoc.DocumentElement;
            XmlElement avestaXml = configs["Avesta"];
            
            if(avestaXml != null)
            {
                string attrVal = avestaXml.Attributes["WavelengthStart"]?.Value ?? "190";
                wavelengthMin = double.Parse(attrVal);

                attrVal = avestaXml.Attributes["WavelengthEnd"]?.Value ?? "1100";
                wavelengthMax = double.Parse(attrVal);

                attrVal = avestaXml.Attributes["B0"]?.Value;
                if (attrVal == null)
                    Index2WavelengthAprox.B0 = wavelengthMax;
                else
                    Index2WavelengthAprox.B0 = double.Parse(attrVal, NumberStyles.Float, CultureInfo.InvariantCulture);

                attrVal = avestaXml.Attributes["B1"]?.Value;
                if (attrVal == null)
                    Index2WavelengthAprox.B1 = (wavelengthMin - wavelengthMax) / parameters.nNumPixels;
                else
                    Index2WavelengthAprox.B1 = double.Parse(attrVal, NumberStyles.Float, CultureInfo.InvariantCulture);

                attrVal = avestaXml.Attributes["B2"]?.Value;
                if (attrVal == null)
                    Index2WavelengthAprox.B2 = 0;
                else
                    Index2WavelengthAprox.B2 = double.Parse(attrVal, NumberStyles.Float, CultureInfo.InvariantCulture);

                attrVal = avestaXml.Attributes["AproxType"]?.Value;

                if (attrVal != null && Enum.TryParse<Aprox.AproxTypes>(attrVal, out Aprox.AproxTypes aproxType))
                    Index2WavelengthAprox.AproxType = aproxType;
                else
                    Index2WavelengthAprox.AproxType = Aprox.AproxTypes.Linear;

                if (wavelengthMin >= wavelengthMax)
                    throw new Exception("Некорректно задан спектральный диапазон прибора");

                if (Index2WavelengthAprox.AproxType == Aprox.AproxTypes.Linear)
                {
                    if (Index2WavelengthAprox.B1 == 0)
                        throw new Exception("B1 не должен быть равен 0");
                }

                if (Index2WavelengthAprox.AproxType == Aprox.AproxTypes.Parabola2)
                {
                    double extr = -Index2WavelengthAprox.B1 / (2 * Index2WavelengthAprox.B2);
                    if (extr <= parameters.nNumPixels-1 && extr >= 0)
                    {
                        throw new Exception("Некорректные значения коэффициентов параболы.\nЭкстремум не должен находиться в рабочем диапазоне спектрометра");
                    }
                }

                sensivityFile = avestaXml.Attributes["SensivityFile"]?.Value ?? "";
                //Считываем кривую чувствительности
                try
                {
                    string[] allLines = File.ReadAllLines(sensivityFile);
                    if (allLines.Length < 3)
                        throw new Exception();

                    string signature = allLines[0];

                    if (signature.ToLower() == "wavelength_sensivity")
                    {
                        sensititvityList.Clear();
                        for (int i = 1; i < allLines.Length; i++)
                        {
                            LineSplitter(allLines[i], out double xi, out double yi);
                            sensititvityList.Add(new DataPoint(xi, yi));
                        }
                        for (int i = 0; i < parameters.nNumPixels; i++)
                        {
                            sensitivitys[i] = SensivityByWavelength(Index2Wavelength(i));
                        }
                    }
                    if (signature.ToLower() == "sensivity_in_pixel")
                    {
                        if (allLines.Length != parameters.nNumPixels)
                            throw new Exception();
                        sensititvityList.Clear();

                        for (int i = 1; i < allLines.Length; i++)
                        {
                            sensitivitys[i] = int.Parse(allLines[i], NumberStyles.Float, CultureInfo.InvariantCulture);
                            sensititvityList.Add(new DataPoint(Index2Wavelength(i), sensitivitys[i]));
                        }
                    }
                }
                catch (Exception ex)
                {
                    sensititvityList.Clear();
                    sensititvityList.Add(new DataPoint(wavelengthMin, 1));
                    sensititvityList.Add(new DataPoint(wavelengthMax, 1));

                    sensitivitys = new double[parameters.nNumPixels];
                    for (int i = 0; i < parameters.nNumPixels; i++)
                    {
                        sensitivitys[i] = SensivityByWavelength(Index2Wavelength(i));
                    }
                }
            }
            //InitSensivityAndPolynom(); //via file

        }

        private static void LineSplitter(string line, out double d0, out double d1)
        {
            string[] values = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            d0 = double.Parse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture);
            d1 = double.Parse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        Aprox Index2WavelengthAprox = new Aprox();

        private double[] sensitivitys = null; //массив чувствительностей ка каждом пикселе линейки
        public double[] Sensitivitys
        {
            get { return sensitivitys; }
            set { sensitivitys = value; }
        }

        public override double Exposure { get => extendedParameters.sExposureTime;
            set
            {
                CCD_SetParameter(id, UsbCCD.PRM_EXPTIME, (float)value);
                CCD_GetExtendParameters(id, ref extendedParameters);
            }
        }

        public double SensivityByWavelength(double wavelength)
        {
            return MMath.Interp(wavelength, sensititvityList);
        }

        public double GetSensivityInPixel(int index)
        {
            return sensititvityList[index].X;
        }

        public double Index2Wavelength(int pixelIndex)
        {
            return Index2WavelengthAprox.Function(pixelIndex);
        }

        public int Wavelength2Index(double wavelength)
        {
            double[] x = Index2WavelengthAprox.FunctionInv(wavelength);
            if (Index2WavelengthAprox.AproxType == Aprox.AproxTypes.Linear)
                return Convert.ToInt32(x[0]);
            if (Index2WavelengthAprox.AproxType == Aprox.AproxTypes.Parabola2)
            {
                if (x[0] >= 0 && x[1] >= 0 && x[0] <= parameters.nNumPixels - 1 && x[1] <= parameters.nNumPixels - 1)
                    throw new Exception("Оба корня внутри диапазона");
                if (x[0] >= 0 && x[0] <= parameters.nNumPixels - 1)
                    return Convert.ToInt32(x[0]);
                else
                if (x[1] >= 0 && x[1] <= parameters.nNumPixels - 1)
                    return Convert.ToInt32(x[1]);
                else
                    throw new Exception("Корни находятся вне границ");
            }
            //default
            return 0;
        }

        public override int AveragesCount
        {
            get => averagesCount;
            set
            {
                if (value <= 0)
                    throw new Exception("Число усреднений должно быть больше 0");

                averagesCount = value;
            }
        }
    }

    public class UsbCCD
    {
        public const string dllname = "CCDUSBDCOM01.dll";

        /// <summary>The function CCD_Init should start before all another function.
        ///This function performs the search of all CCD-cameras and sets the initial parameters
        ///ahAppWnd may be 0. Prm and ID not used;</summary>
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_Init")]
        public static extern bool CCD_Init(IntPtr ahAppWnd, string prm, ref int ID);

        //The function CCD_HitTest is used for hit test of CCD-cameras
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_HitTest")]
        public static extern bool CCD_HitTest(int ID);

        // Cause function CCD_CameraReset when there was a mistake or it is necessary to interrupt registration.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_CameraReset")]
        public static extern bool CCD_CameraReset(int ID);

        //The function CCD_SetParameters is used for CCD-camera parameters' setting.
        //The parameter Prms is structure of type TCCDUSBParams. The declaration of structure
        //TCCDUSBParams defines above.
        //It is allowed to set only following parameters:
        //  - exposure time
        //  - number of readouts
        //  - synchronization mode
        //The remaining parameters is set automatically
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_SetParameters")]
        public static extern bool CCD_SetParameters(int ID, ref TCCDUSBParams Prms);

        //The function CCD_SetExtendParameters is used for CCD-camera parameters' setting.
        //The parameter Prms is structure of type TCCDUSBExtendParams. The declaration of structure
        //TCCDUSBExtendParams defines above.
        //It is allowed to set only following parameters:
        //  - exposure time
        //  - number of readouts
        //  - synchronization mode
        //  - device mode
        //  - strips
        //  - the time preliminary burning
        //  - the raised sensitivity
        //  - shuter time
        //The remaining parameters is set automatically.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_SetExtendParameters")]
        public static extern bool CCD_SetExtendParameters(int ID, ref TCCDUSBExtendParams Prms);

        //The function CCD_GetParameters is used to get the current parameters
        //of CCD-camera.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetParameters")]
        public static extern bool CCD_GetParameters(int ID, ref TCCDUSBParams Prms);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetExtendParameters")]
        public static extern bool CCD_GetExtendParameters(int ID, ref TCCDUSBExtendParams Prms);

        //The function CCD_SetParameter is used to set the parameters of CCD-camera separately
        // If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        // If one device is used then ID = 0.
        // dwPrmID - parameter identification number. Its can take following values of constants:
        // PRM_READOUTS - the number of readouts
        // PRM_EXPTIME  - the exposure time
        // PRM_SYNCHR   - the synchronization mode. In the external synchronization mode the number of
        //                readouts always equals one.
        //Prm - the value of parameter
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_SetParameter")]
        public static extern bool CCD_SetParameter(int ID, uint dwPrmID, float Prm);

        //The function CCD_GetParameter is used to get the parameters of CCD-camera separately
        // If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        // If one device is used then ID = 0.
        // dwPrmID - parameter identification number. Its can take following values of constants:
        // PRM_DIGIT       - The digit capacity of CCD-camera
        // PRM_PIXELRATE   - The pixel timing
        // PRM_NUMPIXELS   - The number of pixels
        // PRM_READOUTS    - The  number of readouts
        // PRM_EXPTIME     - The exposure time
        // PRM_SYNCHR      - The synchronization mode
        //Prm - the returned value of parameter
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetParameter")]
        public static extern bool CCD_GetParameter(int ID, uint dwPrmID, ref float Prm);

        //The function CCD_InitMeasuring must be start before beginning of measuring
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_InitMeasuring")]
        public static extern bool CCD_InitMeasuring(int ID);
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_InitMeasuringCallBack")]
        public static extern bool CCD_InitMeasuringCallBack(int ID, CallBackDataReady callbackfun);

        /// <summary>
        ///The function CCD_InitMeasuringData must be start before beginning of measuring
        ///The size of array must be equal tothe pixels' number of CCD-camera
        ///(nNumPixelsH*nNumPixelsV*nNumReadOuts*SizeOf(DWORD))
        /// </summary>
        /// <param name="ID">If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        /// If one device is used then ID = 0.</param>
        /// <param name="apData">apData is the pointer to array of uint</param>
        /// <returns></returns>

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_InitMeasuringData")]
        public static extern bool CCD_InitMeasuringData(int ID, IntPtr apData);

        /// <summary>
        ///Call CCD_DoneMeasuringData function after the termination of all measurements.
        ///If you plan to begin measurement anew, do not call this function.
        ///It is called automatically at a exit from DLL.
        /// </summary>
        /// <param name="ID">If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        ///If one device is used then ID = 0.</param>
        /// <returns></returns>
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_DoneMeasuring")]
        public static extern bool CCD_DoneMeasuring(int ID);

        //The function CCD_StartWaitMeasuring is used to start and wait the measurement.
        //The function starts the measurement and waits the finishing of the measurement.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_StartWaitMeasuring")]
        public static extern bool CCD_StartWaitMeasuring(int ID);


        //The function CCD_StartMeasuring is used to start the measurement only
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_StartMeasuring")]
        public static extern bool CCD_StartMeasuring(int ID);

        // The function CCD_GetMeasureStatus is used to check status of a measurement.
        // This function is used with the function CCDUSB_StartMeasuring.
        // If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        // If one device is used then ID = 0.
        // dwStatus - the result value can take one of following constants:
        // STATUS_WAIT_DATA  - the measurement in processing
        // STATUS_WAIT_TRIG  - the waiting of synchronization pulse
        // STATUS_DATA_READY - the measurement has been finished
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetMeasureStatus")]
        public static extern bool CCD_GetMeasureStatus(int ID, ref uint adwStatus);

        //The function CCD_GetData is used to get the result of measurement.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        //bData - the pointegr to array of DWORD. The length of array must be equal to
        //the pixels' number of CCD-camera
        //It is applied to cameras with a linear CCD-sensor and function CCDUSB_InitMeasuring.
        //To matrix registrars should apply function CCDUSB_InitMeasuringData.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetData")]
        public static extern bool CCD_GetData(int ID, IntPtr pDataUInt32);
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetDataWord")]
        public static extern bool CCD_GetDataWord(int ID, IntPtr pDataUInt16);
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetDataByte")]
        public static extern bool CCD_GetDataByte(int ID, IntPtr pDataByte);


        //The function CCD_GetSerialNum returns unique serial number of CCD-camera.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetSerialNum")]
        public static extern bool CCD_GetSerialNum(int ID, ref string sernum);

        //The function CCD_GetSerialNumber returns unique serial number of CCD-camera.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetSerialNumber")]
        public static extern string CCD_GetSerialNumber(int ID);

        //The function CCD_GetSensorName returns a device name.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetSensorName")]
        public static extern string CCD_GetSensorName(int ID);

        //The function CCD_GetID allows to receive ID for the chamber with known serial number.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_GetID")]
        public static extern bool CCD_GetID(string sernum, ref int ID);

        //Parameters of a spectroscopic mode of a matrix are established either through function CCDUSB_SetExtendParameters
        //or through functions CCD_ClearStrips, CCD_AddStrip and CCD_DeleteStrip.

        //This function is used for management in parameters of a spectroscopic mode of a CCD-matrix.
        //Function CCD_ClearStrips clears the list of strips.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_ClearStrips")]
        public static extern bool CCD_ClearStrips(int ID);

        //Function CCD_AddStrip adds a strip in the list.
        //Parameters of a strip are specified in arcStrip.
        //The number of strips increases on 1.
        //Strips cannot be blocked.
        //Function returns TRUE if parameters of a strip are correct also a strip is successfully added.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_AddStrip")]
        public static extern bool CCD_AddStrip(int ID, Rectangle rect);

        //Function CCD_DeleteStrip deletes a strip with number Index from the list of strips.
        //The number of strips in the list decreases on 1.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCD_DeleteStrip")]
        public static extern bool CCD_DeleteStrip(int ID, int Index);

        //The function CCDUSB_SetPIN3 control a pin number 3 of the synch sockets.
        //If some devices are used ID is the identifier USB-device. Can be 0, 1, 2.
        //If one device is used then ID = 0.
        //If OnOff = false that on pin 0 V.
        //If OnOff = true  that on pin 5 V.
        //Function CCDUSB_Init reset pin in 0 V.
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDUSB_SetPIN0")]
        public static extern bool CCDUSB_SetPIN0(int ID, bool OnOff);
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDUSB_SetPIN3")]
        public static extern bool CCDUSB_SetPIN3(bool OnOff);
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDUSB_SetPIN4")]
        public static extern bool CCDUSB_SetPIN4(bool OnOff);

        // Генерирует синхроимпульс
        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDUSB_SynchPIN")]
        public static extern bool CCDUSB_SynchPIN();

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDUSB_MemoryWrite")]
        public static extern bool CCDUSB_MemoryWrite(int ID, int aStartAddr, IntPtr aBuffShort, int BuffSize);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDUSB_MemoryRead")]
        public static extern bool CCDUSB_MemoryRead(int ID, int aStartAddr, IntPtr aBuffShort, int BuffSize);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDUSB_MemoryFileWrite")]
        public static extern bool CCDUSB_MemoryFileWrite(int ID, string FileName);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDUSB_MemoryFileRead")]
        public static extern bool CCDUSB_MemoryFileRead(int ID, string FileName);


        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "SetWordUSBExt")]
        public static extern bool SetWordUSBExt(int ID, byte bRequest, short wValue, short wIndex);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDDCOM_DisconectDCOM")]
        public static extern bool CCDDCOM_DisconectDCOM(int ID);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "CCDDCOM_SetDCOMRemoteName")]
        public static extern bool CCDDCOM_SetDCOMRemoteName(int ID, string RemoteName);

        [DllImport(dllname, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "SHUTTERSOLARLS_Set_")]
        public static extern bool SHUTTERSOLARLS_Set_(int ID);


        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct TCCDUSBParams
        {
            public uint dwDigitCapacity;//The digit capacity of CCD-camera
            public int nPixelRate;      //The pixel rate kHz
            public int nNumPixels;      //The number of pixels

            public int nNumReadOuts;    //The number of readouts
            public int nExposureTime;   //The exposure time
            public uint dwSynchr;       //The synchronization mode
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct TCCDUSBExtendParams
        {
            public uint dwDigitCapacity;// The digit capacity of CCD-camera
            public int nPixelRate;      // The pixel rate (kHz)
            public int nNumPixelsH;     // The number of pixels on a horizontal (columns number of CCD-array)
            public int nNumPixelsV;     // The number of pixels on a vertical (rows number of CCD-array)
            public uint Reserve1;       // not used
            public uint Reserve2;       // not used

            public int nNumReadOuts;   // The number of readouts
            public float sPreBurning;  // The Time preliminary burning in seconds.
                                       // Really only at synchronization SYNCHR_CONTR_NEG but not for all cameras!!!
                                       // Use GetDeviceProperty function to receive properties of the device.
                                       // Is used at a spectrum measurements.
            public float sExposureTime;// The exposure time
            public float sTime2;       // The accumelation delay in mksec
            public uint dwSynchr;      // The synchronization mode.
            public bool bSummingMode;  // Turn on(off) summing mode. Not used.

            public uint dwDeviceMode;  // Turn on(off) spectral mode of CCD-array.
                                       // Some cameras on the basis of CCD-matrixes have an additional modes. See dwProperty.
                                       // DP_MODEA2 It is an additional mode of the matrix registrar. If the device has DP_MODEA2 property it is possible to establish dwDeviceMode in value DEVICEMODEA2.
                                       // In mode DEVICEMODES the device works in a spectroscopic mode.
                                       // The photosensitive field of a matrix is broken into some strips. Strips are set by parameters nStripCount and rcStrips.
                                       // While translating the device in mode DEVICEMODES change nNumPixelsH and nNumPixelsV.
            public int nStripCount;    // The number of strips for a spectral mode

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public Rectangle[] rcStrips; // The strips for a spectral mode.//rcStrips        : array[0..8 - 1] of TRect; 
            public int Reserve11;

            public uint dwSensitivity; // Turn on (off) a mode of the raised sensitivity of a CCD-sensor control. Actually if dwProperty & DP_SENSIT <> 0.
            public uint dwProperty;    // The device property.
            public float sShuterTime;  // Shuter time (ms). Active in minimal exposure time.
                                       // Exposure time = MinExp - sShaterTime.
            public uint dwPropertyExt; // // The Extend device property.
            public uint Reserve7;      // not used
            public uint Reserve8;      // not used
            public uint Reserve9;      // not used
            public uint Reserve10;     // not used
        }



        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        public delegate void CallBackDataReady();


        #region constants

        //The parameter identification number
        public const int PRM_DIGIT = 1; //The digit capacity of CCD-camera
        public const int PRM_PIXELRATE = 2; //The pixel timeing
        public const int PRM_NUMPIXELS = 3; //The number of pixels
        public const int PRM_READOUTS = 4; //The number of readouts
        public const int PRM_EXPTIME = 5; //The exposure time
        public const int PRM_SYNCHR = 6; //The synchronization mode

        public const int PRM_NUMPIXELSH = 7;  //The number of pixels on a horizontal (columns number of CCD-array)
        public const int PRM_NUMPIXELSV = 8;  //The number of pixels on a vertical (rows number of CCD-array)
        public const int PRM_SUMMING = 9;  //The summing mode
        public const int PRM_DEVICEMODE = 10; //The device mode
                                              //DEVICEMODEA1 - Matrix mode #1
                                              //DEVICEMODEA2 - Matrix mode #2
                                              //DEVICEMODES  - The spectroscope mode without a strips.
                                              //                The matrix is submitted as one line
        public const int PRM_STRIPCOUNT = 11; //The number of strips for a spectral mode

        public const int PRM_SENSIT = 14;//The sensitivity
        public const int PRM_DEVICEPROPERTY = 15;//The device property.
        public const int PRM_PREBURNING = 16;// The Time preliminary burning in seconds.
                                             // Really only at synchronizationSYNCHR_CONTR_NEG but not for all cameras!!!
                                             // Use GetDeviceProperty function to receive properties of the device.
                                             // Is used  at a spectrum measurements.
        public const int PRM_SHUTERTIME = 17;//

        public const int PRM_STANDLIGHTERDELAY = 18;//
        public const int PRM_STANDLIGHTERTIME = 19;//

        public const int PRM_SUBMODE = 20;
        public const int PRM_BYTEFROMWORD = 21;

        public const int PRM_REGISTERMODE = 22;

        public const int PRM_SSLS_MODE = 23;
        public const int PRM_SSLS_PRIORDELAY = 24;
        public const int PRM_SSLS_DURATION = 25;
        public const int PRM_SSLS_MODE2 = 26;
        public const int PRM_SSLS_PRIORDELAY2 = 27;
        public const int PRM_SSLS_DURATION2 = 28;
        public const int PRM_SSLS_SHUTTERNUM = 29;

        public const int PRM_E2V_ROWSHIFTTIME = 30;
        //public const int PRM_E2V_HORDMODE      = 31;

        public const int PRM_DELAY = 32;
        public const int PRM_DEVICEPROPERTYEXT = 33;//The extented device property. 1

        // Parameters for 08 and [KVADRO_T_DAC,  KITAY_4000_DAC]
        public const int PRM_K2000 = 0x1000;
        public const int PRM_K20001 = 0x1001;
        public const int PRM_K20002 = 0x1002;
        public const int PRM_K20003 = 0x1003;
        public const int PRM_KAdd2000 = 0x1004;
        public const int PRM_KAdd20002 = 0x1005;
        public const int PRM_KAdd20003 = 0x1006;
        public const int PRM_KAdd20004 = 0x1007;
        public const int PRM_Offset2000 = 0x1008;
        public const int PRM_Offset20001 = 0x1009;
        public const int PRM_Offset20002 = 0x1010;
        public const int PRM_Offset20003 = 0x1011;
        public const int PRM_SortPixels = 0x1012;

        public const int PRM_ShiftAGFT = 0x1014;   // Shift AG FT in mks !!!
        public const int PRM_STANDAGPULSE = 0x1015;   // 0 - const, 1 - Impulse
        public const int PRM_STANDSTOPTIME = 0x1016;   //  Stop time in mks !!!

        public const int PRM_STANDLIGTERPULSE = 0x1017; // Lighter is pulse

        public const int PRM_STANDADCFIRSTWORD = 0x1018;
        public const int PRM_STANDADCFIRSTWORD1 = 0x1019;
        public const int PRM_STANDADCFIRSTWORD2 = 0x1020;
        public const int PRM_STANDADCFIRSTWORD3 = 0x1021;


        public const int PRM_KAmpRed = 0x1022;
        public const int PRM_KRed2000 = 0x1022; // 
        public const int PRM_KAmpGreen = 0x1023;
        public const int PRM_KAmpBlue = 0x1024;
        public const int PRM_KAmpRed1 = 0x1025;
        public const int PRM_KAmpGreen1 = 0x1026;
        public const int PRM_KAmpBlue1 = 0x1027;
        public const int PRM_KAmpRed2 = 0x1028;
        public const int PRM_KAmpGreen2 = 0x1029;
        public const int PRM_KAmpBlue2 = 0x1030;
        public const int PRM_KAmpRed3 = 0x1031;
        public const int PRM_KAmpGreen3 = 0x1032;
        public const int PRM_KAmpBlue3 = 0x1033;
        public const int PRM_OffsetRed = 0x1034;
        public const int PRM_OffsetRed2000 = 0x1034; //
        public const int PRM_OffsetGreen = 0x1035;
        public const int PRM_OffsetBlue = 0x1036;
        public const int PRM_OffsetRed1 = 0x1037;
        public const int PRM_OffsetGreen1 = 0x1038;
        public const int PRM_OffsetBlue1 = 0x1039;
        public const int PRM_OffsetRed2 = 0x1040;
        public const int PRM_OffsetGreen2 = 0x1041;
        public const int PRM_OffsetBlue2 = 0x1042;
        public const int PRM_OffsetRed3 = 0x1043;
        public const int PRM_OffsetGreen3 = 0x1044;
        public const int PRM_OffsetBlue3 = 0x1045;


        // The synchronization mode
        public const int SYNCHR_NONE = 0x1;   // Without synchronization.
        public const int SYNCHR_CONTR = 0x20;  // In the beginning of the first accumulation the positive
                                               // pulse of synchronization is formed.
        public const int SYNCHR_CONTR_FRS = 0x4;   // Clock pulse is formed in the beginning of each accumulation.
        public const int SYNCHR_CONTR_NEG = 0x8;   // One pulse of synchronization is formed on all time of registration.
                                                   // A pulse of negative polarity.
        public const int SYNCHR_EXT = 0x10;  // The beginning of the first accumulation is adhered to growing
                                             // front of external clock pulse.
                                             // All other accumulation occur so quickly as it is possible.
                                             // In a limit -- without the misses.
        public const int SYNCHR_EXT_FRS = 0x2;   // The beginning of each accumulation is adhered to growing front of clock pulse.
                                                 // How much accumulation, so much clock pulses are expected.


        //The status of measurement
        public const int STATUS_WAIT_DATA = 1; //the measurement in processing
        public const int STATUS_WAIT_TRIG = 2; //the waiting of synchronization pulse
        public const int STATUS_DATA_READY = 3; //the measurement has been finished

        public const int MAXSTRIPS = 8;

        // DEVICE MODE
        // Some cameras on the basis of CCD-matrixes have an additional modes. See dwProperty.
        // DP_MODEA2 It is an additional mode of the matrix registrar. If the device has DP_MODEA2 property it is possible to establish dwDeviceMode in value DEVICEMODEA2.
        // In modeDEVICEMODES the device works in a spectroscopic mode.
        // The photosensitive field of a matrix is broken into some strips. Strips are set by parameters nStripCount and rcStrips.
        // While translating the device in mode DEVICEMODES change nNumPixelsH and nNumPixelsV.
        public const int DEVICEMODEA1 = 0x0002;
        public const int DEVICEMODEA2 = 0x0000;
        public const int DEVICEMODES = 0x0003;
        public const int DEVICEMODESTREAM = 0x0004;
        public const int DEVICEMODEFASTSCAN = 0x0010;
        public const int DEVICEMODESTEND2 = 0x0001;
        public const int DEVICEMODESTREAMBYTE = 0x0020;
        public const int DEVICEMODESTREAMBYTE1 = 0x0040;



        // DEVICEPROPERTY

        public const int DP_SYNCHR_CONTR = 0x00000001; //SYNCHR_CONTR is enaible
        public const int DP_SYNCHR_CONTR_FRS = 0x00000002; //SYNCHR_CONTR_FRS is enaible
        public const int DP_SYNCHR_CONTR_NEG = 0x00000004; //SYNCHR_CONTR_NEG is enaible
        public const int DP_SYNCHR_EXT = 0x00000008; //SYNCHR_EXT is enaible
        public const int DP_SYNCHR_EXT_FRS = 0x00000010; //SYNCHR_EXT_FRS is enaible
        public const int DP_SENSIT = 0x00000020; // The sensor or first sensor has a mode of the raised sensitivity.
        public const int DP_MODEA2 = 0x00000040; // Additional matrix mode of the camera.
        public const int DP_MODES1 = 0x00000080; // Spectroscopic mode of a CCD-matrix.
        public const int DP_MODES2 = 0x00000100; // Spectroscopic mode of a CCD-matrix.
        public const int DP_PREBURNING = 0x00000200; // Opportunity to establish preliminary burning.
        public const int DP_SHUTER = 0x00000400; // Property of an electronic shutter.
        public const int DP_CLOCKCONTROL = 0x00000800; // Control ADC clock frequency (nPixelRate).
        public const int DP_FASTSCAN = 0x00001000; // The "fast scan mode" is enaible
        public const int DP_STREAMSCAN = 0x00002000; // The "stream scan mode" is enaible
        public const int DP_SENSIT2 = 0x00004000; // The second sensor has a mode of the raised sensitivity.
        public const int DP_STENDMODE2 = 0x00008000; // Not used
        public const int DP_STENDLIGHTERCONTROL = 0x00010000; // Not used
        public const int DP_COLORSENSOR = 0x00020000; // Color sensor
        public const int DP_STREAMSCANBYTE = 0x00040000; // The "byte-per-pixel stream scan mode" is enaible
        public const int DP_STREAMSCANBYTE1 = 0x00080000; // The additional "byte-per-pixel stream scan mode" is enaible 
        public const int DP_STREAMSCANBYTE3 = 0x00100000; // The additional "byte-per-pixel stream scan mode" is enaible
        public const int DP_REGISTERMODE = 0x00200000; // Set the QUADRO register mode. ALL, LU, RU, LD, RD
        public const int DP_SPIMEMORY = 0x00400000; // SPI Memory
        public const int DP_SHUTTER2 = 0x00800000; // Extenal Shutter from Solar LS
        public const int DP_SHUTTER3 = 0x01000000; // Extenal Shutter from Solar LS

        //*********************************************************************
        //  Extented DEVICEPROPERTY. See PRM_DEVICEPROPERTYEXT
        //*********************************************************************
        public const int DP_DELAY = 0x00000001; // The accumelation Delay in mksec

        public const int SHUTTERSOLARLS_MODE_OFF = 0x01;
        public const int SHUTTERSOLARLS_MODE_ON = 0x02;
        public const int SHUTTERSOLARLS_MODE_AUTO = 0x04;
        public const int SHUTTERSOLARLS_MODE_INVERT = 0x08;

        public const int NCAMMAX = 3;
        #endregion

        public struct Rectangle
        {
            int Left, Right, Top, Down;
        }
    }
}
