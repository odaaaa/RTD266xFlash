﻿using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RTD266xFlash.BackgroundWorkers
{
    public class ChangeLogoWorker : BaseWorker
    {
        private readonly string _logoFileName;

        public delegate void ChangeLogoeWorkerFinishedEvent(RTD266x.Result result);
        public event ChangeLogoeWorkerFinishedEvent ChangeLogoWorkerFinished;

        private readonly Firmware[] _firmwares =
        {
            new Firmware("KeDei v1.0", 0x260D8, 0x12346, 1507, new[]
            {
                new HashInfo(0, 0x80000, "2319EE74B6A09F62484C62B9500FFD356C2A7142BB6D00A5DDFD9E562562F8F4", new []
                {
                    new HashSkip(0xD263, 1),   // CAdjustBackgroundColor 1
                    new HashSkip(0xD273, 1),   // CAdjustBackgroundColor 2
                    new HashSkip(0x12346, 16), // "HDMI"
                    new HashSkip(0x13A31, 48), // palette
                    new HashSkip(0x14733, 1),  // CShowNote
                    new HashSkip(0x260D8, 903) // logo
                })
            }),
            new Firmware("KeDei v1.1, panel type 1 (SKY035S13B00-14439)", 0x260D8, 0x12346, 1507, new[]
            {
                new HashInfo(0, 0x80000, "B980A13D3472C422FB8E101F6A2BA95DCA0CC2C3D133B8B8B68DF7D5F8FD4AEA", new []
                {
                    new HashSkip(0xD45E, 1),
                    new HashSkip(0xD46E, 1),
                    new HashSkip(0x12346, 16),
                    new HashSkip(0x13A31, 48),
                    new HashSkip(0x14733, 1),
                    new HashSkip(0x260D8, 903)
                })
            }),
            new Firmware("KeDei v1.1, panel type 2 (SKY035S13D-199)", 0x260D8, 0x12346, 1507, new[]
            {
                new HashInfo(0, 0x80000, "F206FB3C359FE9BB57BEADA1D79E054DCD7727A898E800C0EDED27F3183BF79B", new []
                {
                    new HashSkip(0xD2D1, 1),
                    new HashSkip(0xD2E1, 1),
                    new HashSkip(0x12346, 16),
                    new HashSkip(0x13A31, 48),
                    new HashSkip(0x14733, 1),
                    new HashSkip(0x260D8, 903)
                })
            })
        };

        public ChangeLogoWorker(RTD266x rtd, string logoFileName) : base(rtd)
        {
            _logoFileName = logoFileName;
        }

        protected override void _backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (string.IsNullOrEmpty(_logoFileName))
            {
                ReportStatus("Error! No logo input file specified.\r\n");
                e.Result = RTD266x.Result.NotOk;
                return;
            }

            ReportStatus("Checking logo file... ");

            string error;

            if (!FontCoder.CheckFile(_logoFileName, FontCoder.FontWidthKedei, FontCoder.FontHeightKedei, out error))
            {
                ReportStatus($"Error! {error}\r\n");
                e.Result = RTD266x.Result.NotOk;
                return;
            }

            ReportStatus("ok\r\n");
            ReportStatus("Identifying device... ");

            RTD266x.Result result;
            RTD266x.StatusInfo status;

            result = _rtd.ReadStatus(out status);

            if (result != RTD266x.Result.Ok || status.ManufacturerId != 0xC8 || status.DeviceId != 0x12)
            {
                ReportStatus("Error! Cannot identify chip.\r\n");
                e.Result = result;
                return;
            }

            ReportStatus("ok\r\n");
            ReportStatus("Reading firmware...\r\n");

            byte[] firmware;

            result = Read(0, 512 * 1024, out firmware, true);

            if (result != RTD266x.Result.Ok)
            {
                ReportStatus(RTD266x.ResultToString(result) + "\r\n");
                e.Result = result;
                return;
            }

            string backupFirmwareFileName = "firmware-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".bin";

            ReportStatus($"Creating firmware backup file \"{backupFirmwareFileName}\"... ");

            try
            {
                File.WriteAllBytes(backupFirmwareFileName, firmware);
            }
            catch (Exception ex)
            {
                ReportStatus($"Error! Could not save file \"{backupFirmwareFileName}\". {ex.Message}\r\n");
                e.Result = result;
                return;
            }

            ReportStatus("ok\r\n");
            ReportStatus("Checking firmware... ");

            Firmware detectedFirmware = null;

            foreach (Firmware fw in _firmwares)
            {
                if (fw.CheckHash(firmware))
                {
                    detectedFirmware = fw;
                    break;
                }
            }

            if (detectedFirmware == null)
            {
                ReportStatus("Error! Could not detect firmware.\r\n");
                e.Result = result;
                return;
            }

            ReportStatus("ok\r\n");
            ReportStatus($"Detected firmware is {detectedFirmware.Name}\r\n");
            ReportStatus("Converting logo... ");

            FontCoder logo = new FontCoder(FontCoder.FontWidthKedei, FontCoder.FontHeightKedei);

            if (!logo.LoadImage(_logoFileName))
            {
                ReportStatus($"Error! Cannot load logo from \"{_logoFileName}\".\r\n");
                e.Result = result;
                return;
            }

            byte[] logoBytes = logo.Encode();

            if (logoBytes.Length > detectedFirmware.MaxLogoLength)
            {
                ReportStatus("Error! Encoded logo is too long and would overwrite other firmware parts.\r\n");
                e.Result = result;
                return;
            }

            ReportStatus("ok\r\n");
            ReportStatus("Embedding the new logo... ");

            Array.Copy(logoBytes, 0, firmware, detectedFirmware.LogoOffset, logoBytes.Length);

            ReportStatus("ok\r\n");
            ReportStatus("Writing patched sector...\r\n");

            byte[] sector = new byte[4096];
            int sectorAddress = (detectedFirmware.LogoOffset / 4096) * 4096;

            Array.Copy(firmware, sectorAddress, sector, 0, sector.Length);

            if (Write(sectorAddress, sector, true) != RTD266x.Result.Ok)
            {
                e.Result = result;
                return;
            }

            ReportStatus("Finished! Now reboot the display and enjoy your new boot logo :)\r\n");

            e.Result = RTD266x.Result.Ok;
        }

        protected override void _backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ChangeLogoWorkerFinished?.Invoke((RTD266x.Result)e.Result);
        }
    }
}
