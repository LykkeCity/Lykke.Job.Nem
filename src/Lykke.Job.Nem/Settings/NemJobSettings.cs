using System;
using JetBrains.Annotations;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Service.Nem.Settings
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class NemJobSettings
    {
        public DbSettings Db               { get; set; }
        public string     NemUrl           { get; set; }
        public string     HotWalletAddress { get; set; }
        public TimeSpan   Period           { get; set; }
    }
}
