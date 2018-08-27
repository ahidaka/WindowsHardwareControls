using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Windows.Devices.I2c;
using Windows.Devices.Spi;
using Windows.Devices.Enumeration;

using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace DDSensors
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        int measureIntervalMSec = 1000;
        DispatcherTimer measureTimer;
        I2CSensor i2c;
        SPISensor spi;
        UARTSensor uart;

        public MainPage()
        {
            InitializeComponent();

            Configration.UseI2C = false;
            Configration.UseSPI = false;
            Configration.UseUART = true;

            Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (Configration.UseI2C)
            {
                i2c = new I2CSensor();
                i2c.Initialize();
            }
            if (Configration.UseSPI)
            {
                spi = new SPISensor();
                spi.Initialize();
            }
            if (Configration.UseUART)
            {
                uart = new UARTSensor();
                uart.Initialize();
            }
            measureTimer = new DispatcherTimer();
            measureTimer.Interval = TimeSpan.FromMilliseconds(measureIntervalMSec);
            measureTimer.Tick += MeasureTimer_Tick;
            measureTimer.Start();
        }

        private void MeasureTimer_Tick(object sender, object e)
        {
            if (Configration.UseI2C)
            {
                tbTemp.Text = i2c.ReadData().ToString("F1") +" ℃";
            }
            if (Configration.UseSPI)
            {
                tbPress.Text = spi.ReadData().ToString("F1") + " hPa";
            }
            if (Configration.UseUART)
            {
                tbCO2.Text = uart.ReadData().ToString("F0") + " ppm";
            }
        }
    }

    public class Configration
    {
        public static bool UseI2C { get; set; }
        public static bool UseSPI { get; set; }
        public static bool UseUART { get; set; }
    }

    public class I2CSensor
    {
        private I2cDevice i2CDevice;
        private const byte I2C_SENSOR_ADDR = 0x48;
        public async void Initialize()
        {
            var settings = new I2cConnectionSettings(I2C_SENSOR_ADDR);
            settings.BusSpeed = I2cBusSpeed.FastMode;
            var controller = await I2cController.GetDefaultAsync();
            i2CDevice = controller.GetDevice(settings);
        }

        public double ReadData()
        {
            byte[] command = { 0x00 };
            byte[] tempDatas = new byte[2];

            i2CDevice.WriteRead(command, tempDatas);
            uint tempData = (uint)tempDatas[0] << 8 | tempDatas[1];
            double temperature = (tempData >> 5) * 0.125;
            return temperature;
        }
    }
    public class SPISensor
    {
        private const byte CHIP_SELECT = 0;
        private const byte RW_BIT = 0x80;
        private const byte MB_BIT = 0x40;
        private SpiDevice spiDev;
        public async void Initialize()
        {
            SpiConnectionSettings settings = new SpiConnectionSettings(CHIP_SELECT);
            settings.ClockFrequency = 5000000;
            settings.Mode = SpiMode.Mode3; // CPOL = 1, CPHA = 1         
            IReadOnlyList<DeviceInformation> dev
                = await DeviceInformation.FindAllAsync(SpiDevice.GetDeviceSelector());
            spiDev = await SpiDevice.FromIdAsync(dev[0].Id, settings);
            if (spiDev == null)
            {
                Debug.WriteLine("spiDev is null!");
                return;
            }

            byte[] WriteBuf_Addr = new byte[] { 0x0F | RW_BIT | MB_BIT, 0 };
            byte[] ReadBuffer = new byte[2];
            spiDev.TransferFullDuplex(WriteBuf_Addr, ReadBuffer);

            if (ReadBuffer[1] != 0xbd)
            {
                Debug.WriteLine("Who am I failed!");
                return;
            }

            byte[] WriteData = new byte[] { 0x20, 0x90 }; //Power on
            spiDev.Write(WriteData);
        }
        public double ReadData()
        {
            byte[] ReadData0 = new byte[2];
            byte[] ReadData1 = new byte[2];
            byte[] ReadData2 = new byte[2];
            byte[] WriteData = new byte[] { 0x28 | RW_BIT | MB_BIT, 0 };
            spiDev.TransferFullDuplex(WriteData, ReadData0);

            WriteData = new byte[] { 0x29 | RW_BIT | MB_BIT, 0 };
            spiDev.TransferFullDuplex(WriteData, ReadData1);

            WriteData = new byte[] { 0x2A | RW_BIT | MB_BIT, 0 };
            spiDev.TransferFullDuplex(WriteData, ReadData2);

            uint barometer = (uint)(ReadData2[1] << 16);
            barometer |= (uint)(ReadData1[1] << 8);
            barometer |= ReadData0[1];

            return barometer / 4096;
        }
    }
    public class UARTSensor
    {
        private SerialDevice serialDev = null;
        private DataReader dataReader;
        private CancellationTokenSource ReadCancellationTokenSource;
        private IReadOnlyList<DeviceInformation> dev;
        DeviceInformation dI;

        double co2data = 0.0D;
        public double ReadData()
        {
            return co2data;
        }

        public async void Initialize()
        {
            dev = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
            for (int i = 0; i < dev.Count; i++)
            {
                dI = dev[i]; // Get last port
            }
            serialDev = await SerialDevice.FromIdAsync(dI.Id);
            if (serialDev == null)
            {
                Debug.WriteLine("serialDev is null");
                return;
            }
            serialDev.WriteTimeout = TimeSpan.FromMilliseconds(1000);
            serialDev.ReadTimeout = TimeSpan.FromMilliseconds(1000);
            serialDev.BaudRate = 9600;
            serialDev.Parity = SerialParity.None;
            serialDev.StopBits = SerialStopBitCount.One;
            serialDev.DataBits = 8;
            serialDev.Handshake = SerialHandshake.None;

            dataReader = new DataReader(serialDev.InputStream);
            while (true)
            {
                await ReadAsync();
            }
        }

        private int Co2Format(string s)
        {
            //string s = " Z 00483 z 00486\r";
            //    this will pickup   ^^^^^
            int value = 0;
            int zPosition = s.IndexOf('z');
            if (zPosition > 0 && zPosition <= 9)
            {
                zPosition += 2;
                string target = s.Substring(zPosition, 5);
                value = int.Parse(target);
            }
            return value;
        }

        private async Task ReadAsync()
        {
            Task<UInt32> loadAsyncTask;
            uint ReadBufferLength = 18;

            dataReader.InputStreamOptions = InputStreamOptions.Partial;
            loadAsyncTask = dataReader.LoadAsync(ReadBufferLength).AsTask();

            UInt32 bytesRead = await loadAsyncTask;
            if (bytesRead > 0)
            {
                string readData = dataReader.ReadString(bytesRead);

                byte[] data = System.Text.Encoding.ASCII.GetBytes(readData);
                Debug.WriteLine("<" + readData + "> " + bytesRead.ToString() + " bytes");
                co2data = (double)Co2Format(readData);
            }
        }
    }
}
