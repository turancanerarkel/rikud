using System.ComponentModel.DataAnnotations;

namespace Rikud.WebAdmin.ViewModels
{
    public class ExcModValVm
    {
        public int Id { get; set; }
        public int ModId { get; set; }

        // String olarak gelecek değerler
        public string GramAltinAlis { get; set; } = "";
        public string GramAltinSatis { get; set; } = "";
        public string AtaCeyrekAlis { get; set; } = "";
        public string AtaCeyrekSatis { get; set; } = "";
        public string AtaYarimAlis { get; set; } = "";
        public string AtaYarimSatis { get; set; } = "";
        public string AtaLiraAlis { get; set; } = "";
        public string AtaLiraSatis { get; set; } = "";
        public string YirmiIkiAyarAlis { get; set; } = "";
        public string YirmiIkiAyarSatis { get; set; } = "";
        public string ZiynetCeyrekAlis { get; set; } = "";
        public string ZiynetCeyrekSatis { get; set; } = "";
        public string ZiynetYarimAlis { get; set; } = "";
        public string ZiynetYarimSatis { get; set; } = "";
        public string ZiynetLiraAlis { get; set; } = "";
        public string ZiynetLiraSatis { get; set; } = "";

        public DateTime CreatedDate { get; set; }
        public int CreatedBy { get; set; }
        public bool SelectedMod { get; set; }

        public string CreatedByStr { get; set; } = "";
        public string ModAdi { get; set; } = "";


    }
}
