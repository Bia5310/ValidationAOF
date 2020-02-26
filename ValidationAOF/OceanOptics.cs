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

        public OceanOptics(int deviceId)
        {
            id = deviceId;
            wrapper = new NETWrapper();
            wavelengthes = wrapper.getWavelengths(id);
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

        public override void SetAveragesNumber(int averagesNumber)
        {
            if (averagesNumber <= 0)
                throw new Exception("Число усреднений должно быть больше 0");

            wrapper.setScansToAverage(id, averagesNumber);
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
