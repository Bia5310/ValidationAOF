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

        public abstract int AveragesCount { get; set; }

        public virtual double WavelengthMax => wavelengthMax;
        public virtual double WavelengthMin => wavelengthMin;

        public abstract double Exposure { get; set; }

        public abstract List<DataPoint> GetSpectrum();

        protected int id = -1;
        public int ID => id;

        public enum SpectrometerType : int { Avesta, OceanOptics }

        public virtual SpectrometerType Type { get; }

        public static Spectrometer ConnectToFirstDevice()
        {
            Spectrometer spectrometer = null;

            try
            {
                var ooIds = OceanOptics.EnumerateOceanOpticsDeviceIDs();
                if (ooIds.Count > 0)
                {
                    spectrometer = new OceanOptics(ooIds[0]);
                }
            }
            catch (Exception ex)
            {
                
            }

            if (spectrometer == null) //если OceanOptics не был подключен
            {
                spectrometer = Avesta.Connect(0);
            }

            if (spectrometer == null)
                throw new Exception("No connected devices");

            return spectrometer;
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
