using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using AvestaSpectrometr;
using OxyPlot;
using OxyPlot.Series;
using AO_Lib;
using static AO_Lib.AO_Devices;
using Microsoft.Win32;

namespace ValidationAOF
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            backgroundWorkerLivespectr.DoWork += BackgroundWorker_DoWork;
            backgroundWorkerLivespectr.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorkerLivespectr.WorkerReportsProgress = true;
            backgroundWorkerLivespectr.WorkerSupportsCancellation = true;

            sliderWavelength.Tag = textBoxWavelength;
            sliderFrequency.Tag = textBoxFrequency;
            sliderWavenumber.Tag = textBoxWavenumber;
            sliderAttenuation.Tag = textBoxAttenuation;

            worker_sequenceMax = new BackgroundWorker();
            worker_sequenceMax.WorkerReportsProgress = true;
            worker_sequenceMax.WorkerSupportsCancellation = true;
            worker_sequenceMax.DoWork += Worker_sequenceMax_DoWork;
        }

        private void Worker_sequenceMax_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                double start = double.Parse(textBoxStart.Text);
                double end = double.Parse(textBoxEnd.Text);
                double step = double.Parse(textBoxStep.Text);

                float wl_start = (float)start;
                float wl_end = (float)end;
                float wl_step = (float)step;


            }
            catch (Exception) { }



        }

        BackgroundWorker worker_sequenceMax = null;

        PlotViewModel plotViewModel = new PlotViewModel();

        float timeInterval = 50; //ms
        public bool capturing = false;

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int[] data = (int[])e.UserState;

            (chart.ActualModel.Series[0] as OxyPlot.Series.LineSeries).Points.Clear();
            List<DataPoint> dataPoints = new List<DataPoint>();
            for (int i = 0; i < data.Length; i++)
            {
                dataPoints.Add(new DataPoint(i, data[i]));
            }
            (chart.ActualModel.Series[0] as OxyPlot.Series.LineSeries).Points.AddRange(dataPoints);
            chart.ActualModel.InvalidatePlot(true);


        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                DateTime dt = DateTime.Now;
                capturing = true;

                while (!stopCapture)
                {
                    dt = DateTime.Now;
                    if (avesta != null)
                    {
                        int[] data = avesta.GetData();
                        backgroundWorkerLivespectr.ReportProgress(0, data);
                    }

                    while (DateTime.Now.Subtract(dt).TotalMilliseconds <= timeInterval && !stopCapture)
                    {
                        Thread.Sleep(10);
                    }
                }
                stopCapture = false;

            }
            catch (Exception exc)
            {

            }
            capturing = false;
        }

        public Avesta avesta = null;
        private BackgroundWorker backgroundWorkerLivespectr = new BackgroundWorker();
        private bool stopCapture = false;

        private void OnStartCapturing()
        {

        }

        private void OnStopCapturing()
        {

        }

        private void Button_plug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (avesta != null) //если подключено, то отключить
                {
                    avesta = null;
                }
                else
                {
                    avesta = Avesta.ConnectToFitstDevice();

                    tb_exposure.Text = avesta.ExtendParameters.sExposureTime.ToString(CultureInfo.InvariantCulture);
                }
            }
            catch (Exception exc)
            {

            }
        }

        private void Button_acc_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton tgb = sender as ToggleButton;

            if (avesta == null)
            {
                tgb.IsChecked = false;
                return;
            }

            if (tgb.IsChecked == true)
            {
                if (!backgroundWorkerLivespectr.IsBusy)
                {
                    backgroundWorkerLivespectr.RunWorkerAsync();
                    OnStartCapturing();
                }
            }
            if (tgb.IsChecked == false)
            {
                if (backgroundWorkerLivespectr.IsBusy)
                {
                    stopCapture = true;
                    backgroundWorkerLivespectr.CancelAsync();
                    OnStopCapturing();
                }
            }
        }

        private void Button_apply_click(object sender, RoutedEventArgs e)
        {
            float exptime = 0;
            if (float.TryParse(tb_exposure.Text, out exptime))
            {
                UsbCCD.CCD_SetParameter(avesta.ID, UsbCCD.PRM_EXPTIME, exptime);
            }
        }

        private AO_Filter AOFilter = null;
        private bool devLoaded = false;
        private bool attenuationAvailable = false;

        public string InvariantCulture { get; private set; }

        private void ButtonLoadDev_Click(object sender, RoutedEventArgs e)
        {
            if (AOFilter != null)
            {
                OpenFileDialog dialog = new OpenFileDialog();

                if (dialog.ShowDialog() == true)
                {
                    int error = AOFilter.Read_dev_file(dialog.FileName);

                    if (error != 0) //0 = gut
                        MessageBox.Show(AOFilter.Implement_Error(error));
                    devLoaded = error == 0;

                    if (devLoaded)
                        InitSlidersByAOF();
                }
            }
            else
            {
                devLoaded = false;
            }
            State_AOF_DevLoad(devLoaded);
        }

        private void InitSlidersByAOF()
        {

            sliderAttenuation.Value = -10;
            sliderAttenuation.Minimum = -20;
            sliderAttenuation.Maximum = -30;
            sliderAttenuation.Minimum = -40;
            MessageBox.Show(sliderAttenuation.Value.ToString());
        }

        private void ButtonConnectAOF_Click(object sender, RoutedEventArgs e)
        {
            if (AOFilter == null) //значит надо подключить
            {
                AOFilter = AO_Filter.Find_and_connect_AnyFilter();
                if (AOFilter == null)
                    MessageBox.Show("Фильтры не найдены");
                else
                {
                    attenuationAvailable = AOFilter.GetType() == typeof(STC_Filter);
                }
            }
            else //значит надо отключить
            {
                //начать с питания
                if (AOFilter.isPowered)
                    AOFilter.PowerOff();
                AOFilter = null;

                State_AOF_DevLoad(false);
                State_AOF_Power(false);
            }
            State_AOF_Connection(AOFilter != null);
        }

        private void State_AOF_Connection(bool connected)
        {
            buttonConnectAOF.Content = connected ? "Откл." : "Подкл.";

            buttonLoadDev.IsEnabled = connected;
            buttonPower.IsEnabled = false;
        }

        private void State_AOF_DevLoad(bool loaded)
        {
            buttonPower.IsEnabled = loaded;
            sliderFrequency.IsEnabled = loaded;
            sliderWavelength.IsEnabled = loaded;
            sliderWavenumber.IsEnabled = loaded;
            textBoxFrequency.IsEnabled = loaded;
            textBoxWavelength.IsEnabled = loaded;
            textBoxWavenumber.IsEnabled = loaded;

            sliderAttenuation.IsEnabled = attenuationAvailable;
            textBoxAttenuation.IsEnabled = attenuationAvailable;
        }

        private void State_AOF_Power(bool powered)
        {
            buttonPower.Content = powered ? "Откл. пит." : "Вкл. пит.";
        }

        private void ButtonPower_Click(object sender, RoutedEventArgs e)
        {
            if (AOFilter != null)
            {
                if (AOFilter.isPowered)
                    AOFilter.PowerOff();
                else
                    AOFilter.PowerOn();
                State_AOF_Power(AOFilter.isPowered);
            }
        }

        public bool IsValueInTextBoxValid(TextBox textBox)
        {
            float value = 0;
            if (float.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                switch (textBox.Name)
                {
                    case "sliderWavelength":
                        if (AOFilter != null)
                            return AOFilter.WL_Min <= value && value <= AOFilter.WL_Max;
                        else
                            return true;

                    case "sliderWavenumber":
                        return true;
                    case "sliderFrequency":
                        if (AOFilter != null)
                            return AOFilter.HZ_Min <= value && value <= AOFilter.HZ_Max;
                        else
                            return true;

                    case "sliderAttenuation":
                        if (AOFilter != null)
                            return true; //AOFilter.WL_Min <= value && value <= AOFilter.WL_Max;
                        else
                            return true;

                    default:
                        return true;
                }
            }
            else
            {
                return false;
            }
        }

        private Color validColor = Colors.White;
        private Color invalidColor = Colors.Orange;

        public Color TextBoxWavelengthValidationColor => IsValueInTextBoxValid(textBoxWavelength) ? validColor : invalidColor;
        public Color TextBoxWavenumberValidationColor => IsValueInTextBoxValid(textBoxWavenumber) ? validColor : invalidColor;
        public Color TextBoxAttenuationValidationColor => IsValueInTextBoxValid(textBoxAttenuation) ? validColor : invalidColor;
        public Color TextBoxFrequencyValidationColor => IsValueInTextBoxValid(textBoxFrequency) ? validColor : invalidColor;

        private void SliderWavelength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //textBox
            /*textBoxWavelength.BeginInit();
            textBoxWavelength.Text = sliderWavelength.Value.ToString(InvariantCulture);
            textBoxWavelength.EndInit();*/
            //another sliders

        }

        private void InitSlidersAndTextBoxes(double wavelength, double wavenumber, double frequency, double attenuation)
        {
            //textBox
            textBoxWavelength.BeginInit();
            textBoxWavelength.Text = sliderWavelength.Value.ToString(InvariantCulture);
            textBoxWavelength.EndInit();
            //another sliders
        }

        private void SliderWavenumber_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void SliderFrequency_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void SliderAttenuation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void TextBoxWavelength_TextChanged(object sender, TextChangedEventArgs e)
        {
            //MessageBox.Show(textBoxWavelength.Text);
        }

        private void TextBoxWavenumber_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void TextBoxFrequency_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void TextBoxAttenuation_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                switch (sender as TextBox)
                {

                }
            }
        }

        private void Button_CalculateAtten_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_CalculateTrans_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_CaptureMaximumCurve_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_acc_Checked(object sender, RoutedEventArgs e)
        {

        }

        private struct DataToCaptureWidespec
        {
            public int N;
        }

        private void Button_CaptureWidespec_Click(object sender, RoutedEventArgs e)
        {
            if (avesta == null)
            {
                MessageBox.Show("Спектрометр не подключен");
                return;
            }

            DataToCaptureWidespec dataToCapture = new DataToCaptureWidespec { N = 1 };

            if(!int.TryParse(TextBox_CountWidespecAvrg.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out dataToCapture.N))
            {
                MessageBox.Show("Неверное значение количества кривых");
                return;
            }

            if(backgroundWorkerLivespectr.IsBusy)
            {
                backgroundWorkerLivespectr.RunWorkerCompleted += BackgroundWorkerLivespectr_RunWorkerCompleted;
                backgroundWorkerLivespectr.CancelAsync();
                return;
            }

            BackgroundWorker backWorkerCapWideSpec = new BackgroundWorker();
            backWorkerCapWideSpec.DoWork += BackWorkerCapWideSpec_DoWork;
            backWorkerCapWideSpec.RunWorkerCompleted += BackWorkerCapWideSpec_RunWorkerCompleted;
            backWorkerCapWideSpec.RunWorkerAsync(dataToCapture);
        }

        private void BackWorkerCapWideSpec_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            double[] avrgData = (double[])e.Result;

            //На график

            (chart.ActualModel.Series[1] as OxyPlot.Series.LineSeries).Points.Clear();
            List<DataPoint> dataPoints = new List<DataPoint>();
            for (int i = 0; i < avrgData.Length; i++)
            {
                dataPoints.Add(new DataPoint(i, avrgData[i]));
            }
            (chart.ActualModel.Series[1] as OxyPlot.Series.LineSeries).Points.AddRange(dataPoints);
            chart.ActualModel.InvalidatePlot(true);

            ((BackgroundWorker)sender).Dispose();
        }

        private void BackgroundWorkerLivespectr_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            backgroundWorkerLivespectr.RunWorkerCompleted -= BackgroundWorkerLivespectr_RunWorkerCompleted;
            if (!backgroundWorkerLivespectr.IsBusy)
                Button_CaptureWidespec_Click(null, null);
        }

        private void BackWorkerCapWideSpec_DoWork(object sender, DoWorkEventArgs e)
        {
            //Захват широкого спектра

            DataToCaptureWidespec capData = (DataToCaptureWidespec)e.Argument;
            try
            {
                int[][] data = new int[capData.N][];

                for(int i = 0; i < capData.N; i++)
                {
                    data[i] = avesta.GetData();
                }

                if(data.Length > 0)
                {
                    double[] avrData = new double[data[0].Length];
                    for(int i = 0; i < data[0].Length; i++)
                    {
                        for(int j = 0; j < data.Length; j++)
                        {
                            avrData[i] += data[j][i];
                        }
                        avrData[i] /= data.Length;
                    }

                    e.Result = avrData;
                }
                
            }
            catch (Exception) { }
        }


        private int[] WideSpectr = null;

    }

    public class PlotViewModel
    {
        public string Title
        {
            set { lineSeries.Title = value; }
            get { return lineSeries.Title ?? ""; }
        }
        private LineSeries lineSeries;
        public LineSeries LineSeries
        {
            get { return lineSeries; }
        }
        public PlotModel DataPlot { get; set; }
        private double _xValue = 1;
        public PlotViewModel()
        {
            DataPlot = new PlotModel();
            lineSeries = new LineSeries();
            DataPlot.Series.Add(lineSeries);
        }
    }
}
