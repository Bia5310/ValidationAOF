﻿using System;
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
using OxyPlot;
using OxyPlot.Series;
using AO_Lib;
using static AO_Lib.AO_Devices;
using Microsoft.Win32;
using System.Windows.Interop;
using Spectrometer;
using System.Runtime.ExceptionServices;
using System.Security;
using System.IO;
using IvanMath;

namespace ValidationAOF
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public Avesta avesta = null;
        private BackgroundWorker backgroundWorkerLivespectr = new BackgroundWorker();
        private bool stopCapture = false;

        BackgroundWorker worker_sequenceMax = null;

        float timeInterval = 50; //ms
        public bool capturing = false;


        //Данные захвата
        List<DataPoint> CurveSpecWide = new List<DataPoint>();
        List<DataPoint> CurveSpecWidePx = new List<DataPoint>();
        List<DataPoint> CurveSpecMaxes = new List<DataPoint>();
        List<DataPoint> CurveSpecMaxesPx = new List<DataPoint>();
        List<DataPoint> CurveDeviationWL = new List<DataPoint>();
        List<DataPoint> CurveTransmission = new List<DataPoint>();
        List<DataPoint> CurveIntensityFromAtten = new List<DataPoint>(); //actual_WL, real_WL - actual_WL
        List<DataPoint> CurveAttenuation = new List<DataPoint>();

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

            //MessageBox.Show(new Win32Exception().Message);
            //InitCCDUSB();
        }

        [HandleProcessCorruptedStateExceptions]
        private void InitCCDUSB()
        {
            int id = 0;
            string str = "";
            bool inited = UsbCCD.CCD_Init(new WindowInteropHelper(this).Handle, str, ref id);
            //Get serium
            //string serium = "";
            //inited = UsbCCD.CCD_GetSerialNum(0, ref serium);
            
            //MessageBox.Show(serium.ToString());
            //listBox1.Items.Add("Serium number: " + serium);
            //listBox1.Items.Add("SensorName: " + UsbCCD.CCD_GetSensorName(0));
            //Get ID
            //inited = UsbCCD.CCD_GetID(serium, ref ID);
            //listBox1.Items.Add("ID: " + ID.ToString());
            //Fill params
            //ExtendParams = new UsbCCD.TCCDUSBExtendParams();
            //inited = UsbCCD.CCD_GetExtendParameters(0, ref ExtendParams);
            //Params = new UsbCCD.TCCDUSBParams();
            //inited = UsbCCD.CCD_GetParameters(0, ref Params);
            /*
            uint status = 0;
            UsbCCD.CCD_GetMeasureStatus(0, ref status);
            listBox1.Items.Add(status.ToString());
            */
            if (!inited)
                MessageBox.Show("InitError");
        }

        List<DataPoint> CurveLive = new List<DataPoint>();

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
                        //int[] data = avesta.GetData();

                        CaptureAveragedSpectralCurve(countAvrgLive, out double[] avrData);

                        List<DataPoint> points = new List<DataPoint>();
                        for(int i = 0; i < avrData.Length; i++)
                        {
                            points.Add(new DataPoint(avesta.Index2Wavelength(i), avrData[i]));
                        }
                        points.Sort(PointComparer);

                        CurveLive = points;

                        backgroundWorkerLivespectr.ReportProgress(0);
                    }

                    while (DateTime.Now.Subtract(dt).TotalMilliseconds <= timeInterval && !stopCapture)
                    {
                        Thread.Sleep(20);
                    }
                }
                stopCapture = false;

            }
            catch (Exception exc)
            {

            }
            capturing = false;
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            (chart.ActualModel.Series[0] as OxyPlot.Series.LineSeries).Points.AddRange(CurveLive);
            chart.ActualModel.InvalidatePlot(true);
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

        private void OnStartCapturing()
        {

        }

        private void OnStopCapturing()
        {
            
        }

        //[HandleProcessCorruptedStateExceptions]
        //[SecurityCritical]
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
                    if(avesta != null)
                    {
                        try
                        {
                            avesta.LoadConfigFromFile("ConfigFile.xml");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                    //string sensorName = UsbCCD.CCD_GetSensorName(0);
                    //MessageBox.Show(sensorName);
                    tb_exposure.Text = avesta.ExtendParameters.sExposureTime.ToString(CultureInfo.InvariantCulture);
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
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
            if(avesta == null)
            {
                MessageBox.Show("Спектрометр не подключен");
                return;
            }

            float exptime = 0;
            if (float.TryParse(tb_exposure.Text, out exptime))
            {
                UsbCCD.CCD_SetParameter(avesta.ID, UsbCCD.PRM_EXPTIME, exptime);
            }
            /*
            //запомним диапазон работы прибора по длинам волн
            try
            {
                if (double.TryParse(TextBox_StartAvestaWL.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out avesta.wavelength_start) &&
                    double.TryParse(TextBox_EndAvestaWL.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out avesta.wavelength_end))
                {
                    if (avesta.wavelength_end < avesta.wavelength_start)
                    {
                        throw new Exception("Start wavelength > end wavelength");
                    }
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }*/


        }

        private AO_Filter AOFilter = null;
        private bool devLoaded = false;
        private bool attenuationAvailable = false;
        //double avestaStartWL = 0;
        //double avestaEndWL = 0;

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
            sliderWavelength.Minimum = AOFilter.WL_Min;
            sliderWavelength.Maximum = AOFilter.WL_Max;

            sliderFrequency.Minimum = AOFilter.HZ_Min;
            sliderFrequency.Maximum = AOFilter.HZ_Max;
            //sliderFrequency.Value = AOFilter.HZ_Current;
            //textBoxFrequency.Text = sliderFrequency.Value.ToString("F2", CultureInfo.InvariantCulture);

            sliderAttenuation.Maximum = 2500;
            sliderAttenuation.Minimum = 1700;
            //sliderAttenuation.Value = 2500;
            //textBoxAttenuation.Text = sliderAttenuation.Value.ToString("F2", CultureInfo.InvariantCulture);

            sliderWavenumber.Minimum = 1e7f / sliderWavelength.Maximum;
            sliderWavenumber.Maximum = 1e7f / sliderWavelength.Minimum;
            //sliderWavenumber.Value = 1e7f / sliderWavelength.Value;
            //textBoxWavenumber.Text = sliderWavenumber.Value.ToString("F2", CultureInfo.InvariantCulture);

            sliderWavelength.Value = (AOFilter.WL_Max + AOFilter.WL_Min) / 2;
            //textBoxWavelength.Text = sliderWavelength.Value.ToString("F2", CultureInfo.InvariantCulture);
            AutoAtten.IsEnabled = AOFilter.FilterType == FilterTypes.STC_Filter;
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
            AutoAtten.IsEnabled = loaded && (AOFilter.FilterType == FilterTypes.STC_Filter);

            sliderAttenuation.IsEnabled = attenuationAvailable;
            textBoxAttenuation.IsEnabled = attenuationAvailable;
        }

        private void SetRanges()
        {
            
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
            SetViaWavelength((float) sliderWavelength.Value);
        }

        private void SliderWavenumber_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetViaWavelength((float) (1e7f/sliderWavenumber.Value));
        }

        private void SliderFrequency_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(AOFilter != null)
                SetViaWavelength(AOFilter.Get_WL_via_HZ((float) sliderFrequency.Value));
        }

        private void SetViaWavelength(float wl)
        {
            if (AOFilter == null)
            {
                return;
            }

            if (wl >= AOFilter.WL_Min && wl <= AOFilter.WL_Max)
            {
                if (AOFilter.FilterType == FilterTypes.STC_Filter)
                {
                    float attenuation = 0;
                    if (AutoAtten.IsChecked == true && CurveAttenuation.Count != 0)
                    {
                        attenuation = (float)MMath.Interp(wl, CurveAttenuation);
                    }
                    else
                    {
                        attenuation = (float)sliderAttenuation.Value;
                    }

                    if(attenuation >= 1700 && attenuation <= 2500)
                        (AOFilter as STC_Filter).Set_Hz(AOFilter.Get_HZ_via_WL(wl), attenuation);

                    sliderAttenuation.ValueChanged -= SliderAttenuation_ValueChanged;
                    sliderAttenuation.Value = attenuation;
                    sliderAttenuation.ValueChanged += SliderAttenuation_ValueChanged;
                }
                else
                {
                    AOFilter.Set_Wl(wl);
                }
            }

            UpdateSliders();
            UpdateSliderTextBoxes();
        }

        private void SliderAttenuation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(AOFilter == null || AOFilter.FilterType != FilterTypes.STC_Filter)
            {
                return;
            }
            STC_Filter STCFilter = (STC_Filter)AOFilter;

            textBoxAttenuation.Text = sliderAttenuation.Value.ToString("F2", CultureInfo.InvariantCulture);
            float hz = (float)AOFilter.HZ_Current;
            float K = (float)sliderAttenuation.Value;
            if(hz >= AOFilter.HZ_Min && hz <= AOFilter.HZ_Max && K >= 1700 && K <= 2500)
            {
                STCFilter.Set_Hz(hz, K);
            }
        }

        private void UpdateSliders()
        {
            if (AOFilter == null)
            {
                return;
            }

            sliderWavelength.ValueChanged -= SliderWavelength_ValueChanged;
            sliderFrequency.ValueChanged -= SliderFrequency_ValueChanged;
            sliderWavenumber.ValueChanged -= SliderWavenumber_ValueChanged;

            try
            {
                sliderWavelength.Value = AOFilter.WL_Current;
                sliderFrequency.Value = AOFilter.HZ_Current;
                sliderWavenumber.Value = 1e7d / sliderWavelength.Value;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
                return;
            }
            finally
            {
                sliderWavelength.ValueChanged += SliderWavelength_ValueChanged;
                sliderFrequency.ValueChanged += SliderFrequency_ValueChanged;
                sliderWavenumber.ValueChanged += SliderWavenumber_ValueChanged;
            }
        }

        private void UpdateSliderTextBoxes()
        {
            if (AOFilter == null)
            {
                return;
            }

            try
            {
                textBoxWavelength.Text = sliderWavelength.Value.ToString("F2", CultureInfo.InvariantCulture);
                textBoxWavenumber.Text = sliderWavenumber.Value.ToString("F2", CultureInfo.InvariantCulture);
                textBoxFrequency.Text = sliderFrequency.Value.ToString("F3", CultureInfo.InvariantCulture);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
                return;
            }
        }

        private void TextBoxWavelength_TextChanged(object sender, TextChangedEventArgs e)
        {
            
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

        BackgroundWorker backWorkerCalibrateAtten = null;

        private void Button_CalculateAtten_Click(object sender, RoutedEventArgs e)
        {
            DataToCaptureСurves dataToCapture = new DataToCaptureСurves();

            try
            {
                if (avesta == null)
                    throw new Exception("Спектрометр не подключен");

                if (AOFilter == null)
                    throw new Exception("Фильтр не подключен");

                if (AOFilter.FilterType != FilterTypes.STC_Filter)
                    throw new Exception("Данный фильтр не поддерживает коэффициент ослабление");

                if (CurveTransmission.Count == 0)
                    throw new Exception("Отсутствует зависимость коэффициента пропускания");
                
                if (CurveSpecWide.Count == 0)
                    throw new Exception("Отсутствует кривая широкого спектра");

                if (double.TryParse(textBoxStartCalibr.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.startCap) &&
                    double.TryParse(textBoxEndCalibr.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.endCap) &&
                    double.TryParse(textBoxStepCalibr.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.stepCap) &&
                    double.TryParse(textBoxCalibrAccuracy.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.accuracy) &&
                    double.TryParse(textBoxCalibrOptimalTrans.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.transmission))
                {
                    if (dataToCapture.startCap > dataToCapture.endCap)
                        throw new Exception("Начальная длина волны больше конечной или совпадают");

                    if ((dataToCapture.endCap - dataToCapture.startCap) < dataToCapture.stepCap)
                        throw new Exception("Шаг слишком велик");

                    if (dataToCapture.stepCap <= 0)
                        throw new Exception("Шаг должен быть > 0");

                    if (dataToCapture.accuracy <= 0)
                        throw new Exception("Шаг по ослаюлению должен быть > 0");

                    if (dataToCapture.transmission <= 0)
                        throw new Exception("Коэффициент пропускания должен быть больше 0");
                }
                else
                {
                    throw new Exception("Введены неправильные значения параметров захвата");
                }

                Progress_calibrAtten.Visibility = Visibility.Visible;
                backWorkerCalibrateAtten = new BackgroundWorker();
                backWorkerCalibrateAtten.WorkerReportsProgress = true;
                backWorkerCalibrateAtten.WorkerSupportsCancellation = true;
                backWorkerCalibrateAtten.DoWork += BackWorkerCalibrateAtten_DoWork;
                backWorkerCalibrateAtten.ProgressChanged += BackWorkerCalibrateAtten_ProgressChanged;
                backWorkerCalibrateAtten.RunWorkerCompleted += BackWorkerCalibrateAtten_RunWorkerCompleted;
                backWorkerCalibrateAtten.RunWorkerAsync(dataToCapture);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }

        private void BackWorkerCalibrateAtten_DoWork(object sender, DoWorkEventArgs e)
        {
            DataToCaptureСurves dataToCapture = (DataToCaptureСurves)e.Argument;

            CurveAttenuation.Clear();

            for(double w = dataToCapture.startCap; w <= dataToCapture.endCap; w += dataToCapture.stepCap)
            {
                double targetIntensity = dataToCapture.transmission * MMath.Interp(w, CurveSpecWide); //оптимальное пропускание * точка на широком спектре

                CalibrationOnWavelength(w, dataToCapture.accuracy, targetIntensity, out double attenuation);
                CurveAttenuation.Add(new DataPoint(w, attenuation));

                backWorkerCalibrateAtten.ReportProgress(Convert.ToInt32( 100d*(w - dataToCapture.startCap)/(dataToCapture.endCap - dataToCapture.startCap) ));
            }

            CurveAttenuation.Sort(PointComparer);
        }

        private void CalibrationOnWavelength(double wavelength, double accuracy, double target, out double attenuation)
        {
            double att_l = 1700;
            double att_r = 2500;
            double att_mid = (att_l + att_r) / 2d;
            attenuation = 2500;

            double n = 0;
            double nmax = Math.Log( (att_r - att_l) / accuracy, 2) + 5;

            while(n <= nmax)
            {
                att_mid = (att_l + att_r) / 2d;

                if((float) wavelength >= AOFilter.WL_Min && (float) wavelength <= AOFilter.WL_Max && (float)att_mid >= 1700 && (float)att_mid <= 2500)
                {
                    (AOFilter as STC_Filter).Set_Hz(AOFilter.Get_HZ_via_WL((float) wavelength), (float) att_mid);
                }

                int[] data = avesta.GetData();

                int max_ind = 0;
                for(int i = 1; i < data.Length; i++)
                {
                    if (data[i] > data[max_ind])
                        max_ind = i;
                }

                if(data[max_ind] > target)
                {
                    att_l = att_mid;
                }
                else
                {
                    att_r = att_mid;
                }

                n++;

                if(Math.Abs(att_r - att_l) <= accuracy)
                {
                    break;
                }
            }

            attenuation = att_mid = (att_l + att_r) / 2d; //maybe, att_mid will be useful
        }

        private void BackWorkerCalibrateAtten_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Progress_calibrAtten.Value = e.ProgressPercentage;
        }

        private void BackWorkerCalibrateAtten_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Progress_calibrAtten.Visibility = Visibility.Collapsed;


        }

        private void Button_CalculateTrans_Click(object sender, RoutedEventArgs e)
        {
            if(CurveSpecMaxes.Count == 0 || CurveSpecWide.Count == 0)
            {
                TextBlock_StatusTrans.Text = "Не загружены спектральные кривые";
                return;
            }
            
            CurveTransmission = MMath.Divide(CurveSpecMaxes, CurveSpecWide);
            if(CurveTransmission.Count == 0)
            {
                TextBlock_StatusTrans.Text = "Ошибка";
            }
            else
            {
                (chart.ActualModel.Series[3] as OxyPlot.Series.LineSeries).Points.Clear();
                (chart.ActualModel.Series[3] as OxyPlot.Series.LineSeries).Points.AddRange(CurveTransmission);
                chart.ActualModel.InvalidatePlot(true);
                TextBlock_StatusTrans.Text = "Рассчитано";

                double optTransm = CurveTransmission[0].Y;
                for (int i = 1; i < CurveTransmission.Count; i++)
                    if (CurveTransmission[i].Y < optTransm)
                        optTransm = CurveTransmission[i].Y;
                textBoxCalibrOptimalTrans.Text = optTransm.ToString("F3", CultureInfo.InvariantCulture);
                TextBlock_StatusTrans.Text = "Загружен";
            }
        }

        private void Button_CaptureMaximumCurve_Click(object sender, RoutedEventArgs e)
        {
            if(avesta == null)
            {
                MessageBox.Show("Спектрометр не подключен");
                return;
            }

            if(AOFilter == null)
            {
                MessageBox.Show("Фильтр не подключен");
                return;
            }

            DataToCaptureСurves dataToCapture = new DataToCaptureСurves
            {
                startCap = 0,
                endCap = 0,
                stepCap = 0,
                numberOfFrames = 0
            };

            try
            {
                if (avesta.wavelength_start >= avesta.wavelength_end)
                {
                    throw new Exception("Предельный диапазон длин волн спектрометра не введен или введен неверно");
                }

                if (double.TryParse(textBoxStart.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.startCap) &&
                    double.TryParse(textBoxEnd.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.endCap) &&
                    double.TryParse(textBoxStep.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.stepCap) &&
                    int.TryParse(TextBox_CountMaxCurvesAvrg.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.numberOfFrames)) 
                {
                    if (dataToCapture.startCap > dataToCapture.endCap)
                    {
                        throw new Exception("Начальная длина волны больше конечной или совпадают");
                    }
                    if((dataToCapture.endCap - dataToCapture.startCap) < dataToCapture.stepCap)
                    {
                        throw new Exception("Шаг слишком велик");
                    }
                    if(dataToCapture.numberOfFrames < 1)
                    {
                        throw new Exception("Число кадров для усреднения должно быть > 0");
                    }
                    if (dataToCapture.stepCap <= 0)
                    {
                        throw new Exception("Шаг должен быть > 0");
                    }
                    //Проверим, а не слишком ли мал шаг
                    
                    //coming soon
                    
                }
                else
                {
                    throw new Exception("Введены неправильные значения параметров захвата");
                }

                if(avesta.wavelength_end < dataToCapture.endCap || avesta.wavelength_start > dataToCapture.startCap)
                {
                    throw new Exception("Спектрометр не работает в таких границах");
                }
            }
            catch(Exception ex) {
                MessageBox.Show(ex.Message);
                return;
            }

            if(backgroundWorkerLivespectr.IsBusy)
            {
                MessageBox.Show("Остановите захват кривой");
                return;
            }

            Progress_Maxes.Visibility = Visibility.Visible;
            backWorkerCapCurve = new BackgroundWorker();
            backWorkerCapCurve.DoWork += BackWorkerCapCurve_DoWork;
            backWorkerCapCurve.WorkerReportsProgress = true;
            backWorkerCapCurve.WorkerSupportsCancellation = true;
            backWorkerCapCurve.ProgressChanged += BackWorkerCapCurve_ProgressChanged;
            backWorkerCapCurve.RunWorkerCompleted += BackWorkerCapCurve_RunWorkerCompleted;
            backWorkerCapCurve.RunWorkerAsync(dataToCapture);
        }

        private void BackWorkerCapCurve_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Progress_Maxes.Value = e.ProgressPercentage;
        }

        BackgroundWorker backWorkerCapCurve = null;

        private void BackWorkerCapCurve_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if(e.Cancelled)
            {
                TextBlock_StatusMaximumCurve.Text = "Неудачно";
                return;
            }

            //На график
            (chart.ActualModel.Series[2] as OxyPlot.Series.LineSeries).Points.Clear();

            (chart.ActualModel.Series[2] as OxyPlot.Series.LineSeries).Points.AddRange(CurveSpecMaxes);
            chart.ActualModel.InvalidatePlot(true);

            ((BackgroundWorker)sender).Dispose();

            TextBlock_StatusMaximumCurve.Text = "Захвачено " + CurveSpecMaxes.Count.ToString() + " точек";
            Progress_Maxes.Visibility = Visibility.Collapsed;
        }

        private void BackWorkerCapCurve_DoWork(object sender, DoWorkEventArgs e) //Захватывает кривую спектральных максимумов
        {
            DataToCaptureСurves dataToCapture = (DataToCaptureСurves) (e.Argument);

            try
            {
                double actual_WL = dataToCapture.startCap;

                /*List<PointD> listCurvesMax = new List<PointD>();
                listCurvesMax.Clear();*/

                CurveSpecMaxes.Clear();
                CurveSpecMaxesPx.Clear();
                CurveDeviationWL.Clear();

                while (actual_WL <= dataToCapture.endCap)
                {
                    // 1) - set wavelength
                    float wl = (float)actual_WL;
                    if (wl >= AOFilter.WL_Min && wl <= AOFilter.WL_Max)
                    {
                        AOFilter.Set_Wl(wl);
                    }
                    else
                        break;

                    // 2) Снять усредненную кривую
                    CaptureAveragedSpectralCurve(dataToCapture.numberOfFrames, out double[] avrData);
                    // 3) Учесть чувствительность матрицы &
                    // 4) Определить точку максимума (длину волны)

                    double minValue = 0;
                    int minIndex = 0;
                    double maxValue = 0;
                    int maxIndex = 0;
                    double value = 1;
                    for(int i = 0; i < avrData.Length; i++)
                    {
                        value = avrData[i] / avesta.Sensitivitys[i];
                        if (i == 0)
                        {
                            minValue = value;
                            maxValue = value;
                        }

                        if (value > maxValue)
                        {
                            maxValue = value;
                            maxIndex = i;
                        }
                        if (value < minValue)
                        {
                            minValue = value;
                            minIndex = i;
                        }
                    }

                    // 5) Добавить точку в список
                    double real_WL = avesta.Index2Wavelength(maxIndex); //длина волны, на которой получился максимум в реальности

                    CurveSpecMaxesPx.Add(new DataPoint(maxIndex, maxValue));
                    CurveSpecMaxes.Add(new DataPoint(real_WL, maxValue));
                    CurveDeviationWL.Add(new DataPoint(actual_WL, real_WL - actual_WL));

                    CurveSpecMaxesPx.Sort(PointComparer);
                    CurveSpecMaxes.Sort(PointComparer);
                    CurveDeviationWL.Sort(PointComparer);

                    actual_WL += dataToCapture.stepCap;
                    backWorkerCapCurve.ReportProgress(Convert.ToInt32(100*(actual_WL-dataToCapture.startCap)/(dataToCapture.endCap-dataToCapture.startCap)));
                }

                e.Result = CurveSpecMaxes;
            }
            catch(Exception ex)
            {

            }
            
        }

        private void Button_acc_Checked(object sender, RoutedEventArgs e)
        {

        }

        private struct DataToCaptureСurves
        {
            public double startCap;
            public double endCap;
            public double stepCap;
            public int numberOfFrames;
            public double wavelength;
            public double accuracy;
            public int N;
            public double transmission;
        }

        private void Button_CaptureWidespec_Click(object sender, RoutedEventArgs e)
        {
            if (avesta == null)
            {
                MessageBox.Show("Спектрометр не подключен");
                return;
            }

            DataToCaptureСurves dataToCapture = new DataToCaptureСurves{ N = 1 };

            if(!int.TryParse(TextBox_CountWidespecAvrg.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out dataToCapture.N))
            {
                MessageBox.Show("Неверное значение количества кривых");
                return;
            }

            if(backgroundWorkerLivespectr.IsBusy)
            {
                //backgroundWorkerLivespectr.RunWorkerCompleted += BackgroundWorkerLivespectr_RunWorkerCompleted;
                //backgroundWorkerLivespectr.CancelAsync();
                MessageBox.Show("Остановите запись перед началом захвата кривой");
                return;
            }

            BackgroundWorker backWorkerCapWideSpec = new BackgroundWorker();
            backWorkerCapWideSpec.WorkerReportsProgress = true;
            backWorkerCapWideSpec.WorkerSupportsCancellation = true;
            backWorkerCapWideSpec.ProgressChanged += BackWorkerCapWideSpec_ProgressChanged;
            backWorkerCapWideSpec.DoWork += BackWorkerCapWideSpec_DoWork;
            backWorkerCapWideSpec.RunWorkerCompleted += BackWorkerCapWideSpec_RunWorkerCompleted;
            backWorkerCapWideSpec.RunWorkerAsync(dataToCapture);
        }

        private void BackWorkerCapWideSpec_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            
        }

        private void BackWorkerCapWideSpec_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if(e.Cancelled)
            {
                TextBlock_StatusWidespec.Text = "Неудачно";
                return;
            }

            double[] avrgData = (double[])e.Result;
            
            //На график

            (chart.ActualModel.Series[1] as OxyPlot.Series.LineSeries).Points.Clear();
            /*List<DataPoint> dataPoints = new List<DataPoint>();
            for (int i = 0; i < avrgData.Length; i++)
            {
                dataPoints.Add(new DataPoint(avesta.Index2Wavelength(i), avrgData[i]));
            }*/
            (chart.ActualModel.Series[1] as OxyPlot.Series.LineSeries).Points.AddRange(CurveSpecWide);
            chart.ActualModel.InvalidatePlot(true);

            ((BackgroundWorker)sender).Dispose();

            TextBlock_StatusWidespec.Text = "Захвачено " + avrgData.Length.ToString() + " точек";
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

            DataToCaptureСurves capData = (DataToCaptureСurves)e.Argument;
            try
            {
                CaptureAveragedSpectralCurve(capData.N, out double[] avrData);
                e.Result = avrData;

                CurveSpecWidePx.Clear();
                CurveSpecWide.Clear();
                for(int i = 0; i < avrData.Length; i++)
                {
                    CurveSpecWidePx.Add(new DataPoint(i, avrData[i]));
                    CurveSpecWide.Add(new DataPoint(avesta.Index2Wavelength(i), avrData[i]));
                }

                CurveSpecWidePx.Sort(PointComparer);
                CurveSpecWide.Sort(PointComparer);
            }
            catch (Exception) { }
        }

        private static int PointComparer(DataPoint p1, DataPoint p2)
        {
            if (p1.X > p2.X)
                return 1;
            if (p1.X < p2.X)
                return -1;
            return 0;
        }

        private void CaptureAveragedSpectralCurve(in int N, out double[] avrData, bool correctSensivity = true)
        {
            int[][] data = new int[N][];
            
            for (int i = 0; i < N; i++)
            {
                data[i] = avesta.GetData(correctSensivity);
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
        }

        private void Capture_I_K_Click(object sender, RoutedEventArgs e)
        {
            //захват зависимости I от k

            if (avesta == null)
            {
                MessageBox.Show("Спектрометр не подключен");
                return;
            }

            if (AOFilter == null)
            {
                MessageBox.Show("Фильтр не подключен");
                return;
            }

            if (AOFilter.FilterType != FilterTypes.STC_Filter)
            {
                MessageBox.Show("Данная опеация не поддерживается этим типом фильтра");
                return;
            }

            DataToCaptureСurves dataToCapture = new DataToCaptureСurves
            {
                startCap = 0,
                endCap = 0,
                stepCap = 0,
                numberOfFrames = 1,
                wavelength = AOFilter.WL_Current,
            };

            try
            {
                if (avesta.wavelength_start >= avesta.wavelength_end)
                {
                    throw new Exception("Предельный диапазон длин волн спектрометра не введен или введен неверно");
                }

                if (double.TryParse(textBoxStartK.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.startCap) &&
                    double.TryParse(textBoxEndK.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.endCap) &&
                    double.TryParse(textBoxStepK.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.stepCap) &&
                    double.TryParse(textBoxWavelength_I_K.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dataToCapture.wavelength))
                {
                    if (dataToCapture.startCap > dataToCapture.endCap)
                    {
                        throw new Exception("Начальная длина волны больше конечной или совпадают");
                    }
                    if ((dataToCapture.endCap - dataToCapture.startCap) < dataToCapture.stepCap)
                    {
                        throw new Exception("Шаг слишком велик");
                    }
                    if (dataToCapture.numberOfFrames < 1)
                    {
                        throw new Exception("Число кадров для усреднения должно быть > 0");
                    }
                    if (dataToCapture.wavelength < AOFilter.WL_Min || dataToCapture.wavelength > AOFilter.WL_Max)
                    {
                        throw new Exception("Длина волны вне диапазона");
                    }
                    if (dataToCapture.stepCap <= 0)
                    {
                        throw new Exception("Шаг должен быть > 0");
                    }
                    //Проверим, а не слишком ли мал шаг

                    //coming soon

                }
                else
                {
                    throw new Exception("Введены неправильные значения параметров захвата");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            if (backgroundWorkerLivespectr.IsBusy)
            {
                MessageBox.Show("Остановите захват кривой");
                return;
            }

            Progress_I_K.Visibility = Visibility.Visible;
            backWorkerCapCurve_I_K = new BackgroundWorker();
            backWorkerCapCurve_I_K.WorkerReportsProgress = true;
            backWorkerCapCurve_I_K.WorkerSupportsCancellation = true;
            backWorkerCapCurve_I_K.DoWork += BackWorkerCapCurve_I_K_DoWork;
            backWorkerCapCurve_I_K.RunWorkerCompleted += BackWorkerCapCurve_I_K_RunWorkerCompleted;
            backWorkerCapCurve_I_K.ProgressChanged += BackWorkerCapCurve_I_K_ProgressChanged;
            backWorkerCapCurve_I_K.RunWorkerAsync(dataToCapture);
        }

        BackgroundWorker backWorkerCapCurve_I_K = null;

        private void BackWorkerCapCurve_I_K_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Progress_I_K.Value = e.ProgressPercentage;
        }

        private void BackWorkerCapCurve_I_K_DoWork(object sender, DoWorkEventArgs e)
        {
            DataToCaptureСurves dataToCapture = (DataToCaptureСurves)e.Argument;

            try
            {
                double actual_K = dataToCapture.startCap;
                //int index = avesta.Wavelength2Index(dataToCapture.wavelength); //правильность зависит от качества калибровки. Лучше искать максимум
                int indexMax = 0;
                // 0) Установить длину волны
                float wl = (float)dataToCapture.wavelength;
                if (wl >= AOFilter.WL_Min && wl <= AOFilter.WL_Max)
                {
                    AOFilter.Set_Wl(wl);
                }
                
                float hz = AOFilter.Get_HZ_via_WL(wl);

                STC_Filter STCFilter = (STC_Filter)AOFilter;

                CurveIntensityFromAtten.Clear();

                while (actual_K <= dataToCapture.endCap)
                {
                    // 1) Установить ослабление
                    if (wl >= AOFilter.WL_Min && wl <= AOFilter.WL_Max && actual_K >= 1700 && actual_K <= 2500)
                    {
                        STCFilter.Set_Hz(hz, (float)actual_K);
                    }
                    else
                        break;

                    // 2) Снять кривую
                    int[] avrData = avesta.GetData();

                    indexMax = 0;
                    for(int i = 1; i< avrData.Length; i++)
                    {
                        if (avrData[i] > avrData[indexMax])
                            indexMax = i;
                    }

                    // 3) Добавить точку в список
                    CurveIntensityFromAtten.Add(new DataPoint(actual_K, avrData[indexMax]));

                    actual_K += dataToCapture.stepCap;
                    
                    backWorkerCapCurve_I_K.ReportProgress(Convert.ToInt32(100*(actual_K - dataToCapture.startCap)/(dataToCapture.endCap - dataToCapture.startCap)));
                }

                e.Result = CurveIntensityFromAtten;
            }
            catch(Exception ex)
            {

            }

        }

        private void BackWorkerCapCurve_I_K_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if(e.Cancelled)
            {
                MessageBox.Show(e.Error.Message);
                textBlock_Status_I_K.Text = "Неудача";
                return;
            }

            textBlock_Status_I_K.Text = "Захвачено " + CurveIntensityFromAtten.Count.ToString() + " точек";
            Progress_I_K.Visibility = Visibility.Collapsed;
        }

        private void B_Save_I_K_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurveIntensityFromAtten.Count > 0)
                {
                    SaveCurve2File(CurveIntensityFromAtten, NewFileName("Curve_I_K","txt"));
                }
                else
                    throw new Exception("Кривая отсутствует");
            }
            catch(Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void B_I_K_ShowPlot_Click(object sender, RoutedEventArgs e)
        {

        }

        public static void SaveCurve2File(List<DataPoint> dataPoints, string filename)
        {
            using (var sw = new StreamWriter(filename))
            {
                for(int i = 0; i < dataPoints.Count; i++)
                {
                    sw.WriteLine(dataPoints[i].X.ToString("F", CultureInfo.InvariantCulture) + ' ' + dataPoints[i].Y.ToString("F", CultureInfo.InvariantCulture));
                }

                sw.Dispose();
            }
        }

        public static bool LoadCurveFromFile(List<DataPoint> curve, bool sort = false)
        {
            var dialog = new OpenFileDialog();
            dialog.DefaultExt = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            if(dialog.ShowDialog() == true)
            {
                using (var sr = new StreamReader(dialog.FileName))
                {
                    curve.Clear();
                    string line = "";
                    while (!sr.EndOfStream)
                    {
                        line = sr.ReadLine();
                        curve.Add(ParsePointPair(line));
                    }

                    if (sort)
                        curve.Sort(PointComparer);
                }
                return true;
            }
            return false;
        }

        public static DataPoint ParsePointPair(string pair)
        {
            string[] values = pair.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return new DataPoint(double.Parse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture), double.Parse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        private void Button_SaveTrans_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurveTransmission.Count > 0)
                {
                    SaveCurve2File(CurveTransmission, NewFileName("Curve_Transmission","txt"));
                }
                else
                    throw new Exception("Кривая отсутствует");
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void TextBoxWavelength_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                double value = 0;
                if (double.TryParse(textBoxWavelength.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    sliderWavelength.Value = value;
                }
            }
        }

        private void TextBoxAttenuation_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                double value = 0;
                if (double.TryParse(textBoxAttenuation.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    sliderAttenuation.Value = value;
                }
            }
        }

        private void textBoxWavenumber_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                double value = 0;
                if (double.TryParse(textBoxWavenumber.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    sliderWavenumber.Value = value;
                }
            }
        }

        private void textBoxFrequency_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                double value = 0;
                if (double.TryParse(textBoxFrequency.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    sliderFrequency.Value = value;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonSaveWideSpec_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurveSpecWide.Count > 0)
                {
                    SaveCurve2File(CurveSpecWide, NewFileName("Curve_WideSpec","txt"));
                    SaveCurve2File(CurveSpecWidePx, NewFileName("Curve_WideSpecPx","txt"));
                }
                else
                    throw new Exception("Кривая отсутствует");
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void ButtonLoadWideSpec_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(LoadCurveFromFile(CurveSpecWide, true))
                {
                    (chart.ActualModel.Series[0] as OxyPlot.Series.LineSeries).Points.Clear();
                    (chart.ActualModel.Series[0] as OxyPlot.Series.LineSeries).Points.AddRange(CurveSpecWide);
                    chart.ActualModel.InvalidatePlot(true);

                    TextBlock_StatusWidespec.Text = "Загружено " + CurveSpecWide.Count.ToString() + " точек";
                }
            }
            catch(Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void ReloadConfig_Click(object sender, RoutedEventArgs e)
        {
            if(avesta != null)
            {
                try
                {
                    avesta.LoadConfigFromFile("ConfigFile.xml");
                }
                catch(Exception exc)
                {
                    MessageBox.Show(exc.Message);
                }
            }
        }

        private void Button_SaveCurveMaxes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurveSpecMaxes.Count > 0)
                {
                    SaveCurve2File(CurveSpecMaxes, NewFileName("Curve_Maxes","txt"));
                    SaveCurve2File(CurveSpecMaxesPx, NewFileName("Curve_MaxesPx","txt"));
                    SaveCurve2File(CurveDeviationWL, NewFileName("Curve_Deviations","txt"));
                }
                else
                    throw new Exception("Кривая отсутствует");
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void Button_LoadCurveMaxes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(LoadCurveFromFile(CurveSpecMaxes, true))
                {
                    (chart.ActualModel.Series[1] as OxyPlot.Series.LineSeries).Points.Clear();
                    (chart.ActualModel.Series[1] as OxyPlot.Series.LineSeries).Points.AddRange(CurveSpecMaxes);
                    chart.ActualModel.InvalidatePlot(true);

                    TextBlock_StatusMaximumCurve.Text = "Загружено " + CurveSpecMaxes.Count.ToString() + " точек";
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void AutoAtten_Click(object sender, RoutedEventArgs e)
        {
            SetViaWavelength((float) sliderWavelength.Value);
        }

        private void Button_LoadTrans_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(LoadCurveFromFile(CurveTransmission, true))
                {
                    (chart.ActualModel.Series[3] as OxyPlot.Series.LineSeries).Points.Clear();
                    (chart.ActualModel.Series[3] as OxyPlot.Series.LineSeries).Points.AddRange(CurveTransmission);
                    chart.ActualModel.InvalidatePlot(true);

                    double optTransm = CurveTransmission[0].Y;
                    for (int i = 1; i < CurveTransmission.Count; i++)
                        if (CurveTransmission[i].Y < optTransm)
                            optTransm = CurveTransmission[i].Y;
                    textBoxCalibrOptimalTrans.Text = optTransm.ToString("F3", CultureInfo.InvariantCulture);
                    TextBlock_StatusTrans.Text = "Загружен";
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void Button_SaveAtten_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurveAttenuation.Count > 0)
                {
                    SaveCurve2File(CurveAttenuation, NewFileName("Curve_Attenuation","txt"));
                }
                else
                    throw new Exception("Кривая отсутствует");
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void Butto_LoadAtten_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(LoadCurveFromFile(CurveAttenuation, true))
                {
                    TextBlock_StatusAtten.Text = "Загружено" + CurveAttenuation.Count.ToString() + " точек.";
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }

        }

        private int countAvrgLive = 1;

        private void TextBox_CountAvrgLive_TextChanged(object sender, TextChangedEventArgs e)
        {
            int value = 1;
            if(int.TryParse(TextBox_CountAvrgLive.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                if (value > 0)
                    countAvrgLive = value;
            }
        }

        private void Button_SaveLive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurveLive.Count > 0)
                {
                    SaveCurve2File(CurveLive, NewFileName("Curve_Live","txt"));
                }
                else
                    throw new Exception("Кривая отсутствует");
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="basisName">Example = Curve</param>
        /// <param name="ext">Example = txt</param>
        /// <returns></returns>
        private static string NewFileName(string basisName, string ext)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
                FileInfo[] names = di.GetFiles(basisName + "*." + ext);

                if(names.Length != 0)
                {
                    int max_number = -1;

                    for(int i = 0; i < names.Length; i++)
                    {
                        string num_part = Path.GetFileNameWithoutExtension(names[i].FullName).Split('_').Last();

                        int number = 0;
                        if(int.TryParse(num_part, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                        {
                            if (number > max_number)
                                max_number = number;
                        }
                    }
                    max_number++;

                    return basisName + '_' + max_number.ToString(CultureInfo.InvariantCulture) + '.' + ext;
                }
            }
            catch (Exception ex) { }

            return basisName + '.' + ext;
        }
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

    public class PointD
    {
        private double x = 0, y = 0;
        public double X
        {
            get { return x; }
            set { x = value; }
        }
        public double Y
        {
            get { return y; }
            set { y = value; }
        }

        public PointD()
        {
            x = 0; y = 0;
        }

        public PointD(double X, double Y)
        {
            x = X;
            y = Y;
        }
    }
}
