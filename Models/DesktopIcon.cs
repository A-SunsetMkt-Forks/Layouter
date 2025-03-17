using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Layouter.Models
{
    public class DesktopIcon : ObservableObject
    {
        private string _id;
        private string _name;
        private string _iconPath;
        private Point _position;
        private Size _size;
        private bool _isDragging;

        public string Id 
        { 
            get => _id; 
            set => SetProperty(ref _id, value); 
        }

        public string Name 
        { 
            get => _name; 
            set => SetProperty(ref _name, value); 
        }

        public string IconPath 
        { 
            get => _iconPath; 
            set => SetProperty(ref _iconPath, value); 
        }

        public Point Position 
        { 
            get => _position; 
            set => SetProperty(ref _position, value); 
        }

        public Size Size 
        { 
            get => _size; 
            set => SetProperty(ref _size, value); 
        }
        
        public bool IsDragging
        {
            get => _isDragging;
            set => SetProperty(ref _isDragging, value);
        }

        public DesktopIcon()
        {
            // 生成唯一ID
            Id = Guid.NewGuid().ToString();
            Size = new Size(64, 64); // 默认大小
        }

        public DesktopIcon Clone()
        {
            return new DesktopIcon
            {
                Name = this.Name,
                IconPath = this.IconPath,
                Position = this.Position,
                Size = this.Size
                // 注意：不复制Id，让克隆对象拥有新的Id
            };
        }
    }
}
