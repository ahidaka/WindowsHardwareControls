using System;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Power;

namespace HelloBattery
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            BatteryReport();
        }

        private void BatteryReport()
        {
            var battery = Battery.AggregateBattery;
            var report = battery.GetReport();
            double dMaximum, dValue;

            TextStatus.Text = "Battery Status: " + report.Status.ToString();
            if (report.Status != Windows.System.Power.BatteryStatus.NotPresent)
            {
                TextRate.Text = "Charge rate (mW): " + report.ChargeRateInMilliwatts.ToString();
                TextCapacity.Text = "Full capacity (mWh): " + report.FullChargeCapacityInMilliwattHours.ToString();
                TextRemain.Text = "Remain capacity (mWh): " + report.RemainingCapacityInMilliwattHours.ToString();

                dMaximum = Convert.ToDouble(report.FullChargeCapacityInMilliwattHours);
                dValue = Convert.ToDouble(report.RemainingCapacityInMilliwattHours);
                TextPercent.Text = "Chargerd capacity rate: " + ((dValue / dMaximum) * 100).ToString("F2") + "%";
            }
        }
    }
}
