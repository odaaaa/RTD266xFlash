﻿namespace RTD266xFlash
{
    public class Firmware
    {
        /// <summary>
        /// Firmware name
        /// </summary>
        public string Name
        {
            get;
        }

        /// <summary>
        /// Logo offset
        /// </summary>
        public int LogoOffset
        {
            get;
        }

        /// <summary>
        /// "HDMI" string offset
        /// </summary>
        public int HdmiStringOffset
        {
            get;
        }

        /// <summary>
        /// Maximum logo length
        /// </summary>
        public int MaxLogoLength
        {
            get;
        }

        /// <summary>
        /// Hashes for identifying the firmware
        /// </summary>
        public HashInfo[] Hashes
        {
            get;
        }

        public Firmware(string name, int logoOffset, int hdmiStringOffset, int maxLogoLength, HashInfo[] hashes)
        {
            Name = name;
            LogoOffset = logoOffset;
            HdmiStringOffset = hdmiStringOffset;
            MaxLogoLength = maxLogoLength;
            Hashes = hashes;
        }

        /// <summary>
        /// Checks if all of the hashes matches the specified firmware
        /// </summary>
        /// <param name="firmware">Firmware</param>
        /// <returns>True if all hashes match, otherwise false</returns>
        public bool CheckHash(byte[] firmware)
        {
            foreach (HashInfo hash in Hashes)
            {
                string firmwareHash = hash.GetHash(firmware);

                if (string.IsNullOrEmpty(firmwareHash))
                {
                    return false;
                }

                if (firmwareHash != hash.Hash)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
