using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PewsClient
{
    class StationInfoView : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private int m_mmi = 0;
        public int Mmi
        {
            get => m_mmi;
            set
            {
                if (m_mmi != value)
                {
                    m_mmi = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(MmiString));
                }
            }
        }

        public string MmiString => Earthquake.MMIToString(Mmi);

        private Brush m_mmiBrush = Brushes.Black;
        public Brush MmiBrush
        {
            get => m_mmiBrush;
            set
            {
                if (m_mmiBrush != value)
                {
                    m_mmiBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        private Brush m_mmiBackBrush = Brushes.White;
        public Brush MmiBackBrush
        {
            get => m_mmiBackBrush;
            set
            {
                if (m_mmiBackBrush != value)
                {
                    m_mmiBackBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        private string m_name = string.Empty;
        public string Name
        {
            get => m_name;
            set
            {
                if (m_name != value)
                {
                    m_name = value;
                    OnPropertyChanged();
                }
            }
        }

        private string m_location = string.Empty;
        public string Location
        {
            get => m_location;
            set
            {
                if (m_location != value)
                {
                    m_location = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
