using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IvanMath
{
    class MMath
    {
        public static double Interp(double x, DataPoint p1, DataPoint p2)
        {
            return (p2.Y - p1.Y) / (p2.X - p1.X) * (x - p1.X) + p1.Y; 
        }

        public static double Interp(double x, double x1, double y1, double x2, double y2)
        {
            return (y2 - y1) / (x2 - x1) * (x - x1) + y1;
        }

        public static double Interp(double x, List<DataPoint> list)
        {
            if (x <= list[0].X)
                return list[0].Y;
            if (x >= list.Last().X)
                return list.Last().Y;

            int i1 = 0;
            int i2 = list.Count-1;
            double x1 = list[i1].X;
            double x2 = list[i2].X;

            int i = (int)Interp(x, x1, i1, x2, i2);

            while(true)
            {
                i = (int)Interp(x, x1, i1, x2, i2);

                if(x >= list[i].X && x <= list[i+1].X)
                {
                    i1 = i;
                    i2 = i + 1;
                    x1 = list[i1].X;
                    x2 = list[i2].X;
                    break; //значит мы нашли i1, i2, x1, x2
                }
                else
                {
                    if(x < list[i].X)
                    {
                        i2 = i;
                        x2 = list[i2].X;
                    }
                    if(x > list[i+1].X)
                    {
                        i1 = i + 1;
                        x1 = list[i1].X;
                    }
                }
            }

            return Interp(x, list[i1], list[i2]);
        }
    }

    public class Aprox
    {
        public enum AproxTypes : int { Linear, Parabola2 };
        private AproxTypes aproxType;
        public double B0;
        public double B1;
        public double B2;
        public double B3;

        public delegate double FunctionDelegate(double x);
        public delegate double[] FunctionDelegateInv(double y);
        public FunctionDelegate Function;
        public FunctionDelegateInv FunctionInv;

        public AproxTypes AproxType
        {
            get { return aproxType; }
            set
            {
                aproxType = value;
                switch (value)
                {
                    case AproxTypes.Linear:
                        Function = (x) => B0 + B1 * x;
                        FunctionInv = (y) => new double[]{ (y - B0) / B1 };
                        break;
                    case AproxTypes.Parabola2:
                        Function = (x) => B0 + B1 * x + B2 * x * x;
                        FunctionInv = (y) =>
                        {
                            double D = (B1 * B1 - 4 * B2 * (B0-y));
                            if (D < 0)
                                throw new Exception("Отсутствуют действительные корни");
                            D = Math.Sqrt(D);

                            double[] x = new double[]
                            {
                                (-B1 + D)/(2*B2),
                                (-B1 - D)/(2*B2)
                            };
                            return x;
                        };
                        break;
                }
            }
        }

        public Aprox(AproxTypes aproxType = AproxTypes.Linear)
        {
            AproxType = aproxType;
        }
    }
}
