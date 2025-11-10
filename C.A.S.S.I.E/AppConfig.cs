using System.Runtime.Serialization;

namespace C.A.S.S.I.E
{
    [DataContract]
    public class AppConfig
    {
        [DataMember] public string FolderPath { get; set; }
        [DataMember] public string OutputPath { get; set; }

        [DataMember] public decimal GapMs { get; set; }
        [DataMember] public decimal OverlapMs { get; set; }
        [DataMember] public decimal VoiceDelayMs { get; set; }
        [DataMember] public decimal SpeedPercent { get; set; }
        [DataMember] public decimal PitchSemitones { get; set; }

        [DataMember] public string SentenceInput { get; set; }

        [DataMember] public decimal ReverbLevel { get; set; }

        [DataMember] public bool? EnableBackground { get; set; }
    }
}