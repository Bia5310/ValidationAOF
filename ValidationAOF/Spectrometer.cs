using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spectrometer
{
    public abstract class Spectrometer
    {
        protected double wavelengthMax;
        protected double wavelengthMin;

        public virtual double WavelengthMax => wavelengthMax;
        public virtual double WavelengthMin => wavelengthMin;

        public abstract double Exposure { get; set; }

        public abstract List<DataPoint> GetSpectrum();
        public abstract void SetAveragesNumber(int averagesNumber);

        protected int id = -1;
        public int ID => id;

        public enum SpectrometerType : int { Avesta, OceanOptics }

        public virtual SpectrometerType Type { get; }

        public Spectrometer ConnectToFirstDevice()
        {
            List<DeviceInfo> devices = EnumerateAllDevices();
            if (devices.Count == 0)
                return null;
            else
            {
                if(devices[0].Type == SpectrometerType.Avesta)
                {
                    //return Avesta.Connect(0);
                }
                if(devices[0].Type == SpectrometerType.OceanOptics)
                {
                    return OceanOptics.Connect(0);
                }
            }

            return null;
        }

        public static List<DeviceInfo> EnumerateAllDevices()
        {
            List<DeviceInfo> devices = new List<DeviceInfo>();
            List<int> avestaDevices = Avesta.EnumerateAvestaDevicesIDs();
            List<int> ooDevices = OceanOptics.EnumerateOceanOpticsDeviceIDs();

            for (int i = 0; i < avestaDevices.Count; i++)
                devices.Add(new DeviceInfo(SpectrometerType.Avesta, avestaDevices[i]));
            for (int i = 0; i < ooDevices.Count; i++)
                devices.Add(new DeviceInfo(SpectrometerType.OceanOptics, ooDevices[i]));

            return devices;
        }

        public struct DeviceInfo
        {
            public SpectrometerType Type;
            public int ID;

            public DeviceInfo(SpectrometerType type, int id)
            {
                Type = type;
                ID = id;
            }
        }
    }
}
