using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxyPlot;
using OmniDriver;

namespace Spectrometer
{
    class OceanOptics : Spectrometer
    {
        private static NETWrapper wrapper = null;
        private double[] wavelengthes = null;
        private static int devicesCreated = 0;

        static OceanOptics()
        {
            wrapper = new NETWrapper();
        }

        public OceanOptics(int deviceId)
        {
            id = deviceId;
            wavelengthes = wrapper.getWavelengths(id);

            devicesCreated++;

            if (devicesCreated == 1)
                wrapper.openAllSpectrometers();
        }

        ~OceanOptics()
        {
            devicesCreated--;
            try
            {
                wrapper.closeAllSpectrometers();
            }
            catch (Exception) { }
            
        }

        public override SpectrometerType Type => SpectrometerType.OceanOptics;

        public override double Exposure { get => wrapper.getIntegrationTime(id); set => wrapper.setIntegrationTime(id, (int)(1000*value)); }

        public static OceanOptics Connect(int deviceID)
        {
            OceanOptics device = new OceanOptics(deviceID);
            return device;
        }

        public override List<DataPoint> GetSpectrum()
        {
            double[] spectrum = wrapper.getSpectrum(id);
            List<DataPoint> dataPoints = new List<DataPoint>();
            for(int i = 0; i < spectrum.Length; i++)
            {
                dataPoints.Add(new DataPoint(wavelengthes[i], spectrum[i]));
            }
            return dataPoints;
        }

        public override int AveragesCount
        {
            get => wrapper.getScansToAverage(id);
            set
            {
                if (value <= 0)
                    throw new Exception("Число усреднений должно быть больше 0");

                wrapper.setScansToAverage(id, value);
            }
        }

        public static List<int> EnumerateOceanOpticsDeviceIDs()
        {
            List<int> devices = new List<int>();
            
            wrapper.openAllSpectrometers();
            int n = wrapper.getNumberOfSpectrometersFound();
            for (int i = 0; i < n; i++)
                devices.Add(i);
            return devices;
        }
    }
}
