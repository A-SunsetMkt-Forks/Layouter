using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Layouter.ViewModels
{
    public class PartitionItemViewModel : ObservableObject
    {
        private string partitionId;
        private string title;
        private bool isVisible;
        private bool isLocked;
        private bool isSelected;

        /// <summary>
        /// 分区ID
        /// </summary>
        public string PartitionId
        {
            get => partitionId;
            set => SetProperty(ref partitionId, value);
        }

        /// <summary>
        /// 分区标题
        /// </summary>
        public string Title
        {
            get => title;
            set => SetProperty(ref title, value);
        }

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible
        {
            get => isVisible;
            set => SetProperty(ref isVisible, value);
        }

        /// <summary>
        /// 是否锁定
        /// </summary>
        public bool IsLocked
        {
            get => isLocked;
            set => SetProperty(ref isLocked, value);
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }
    }
}
