﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Layouter.Models
{
    public class PartitionDataDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public WindowPositionDto WindowPosition { get; set; }
        public List<IconDataDto> Icons { get; set; }
    }

    public class WindowPositionDto
    {
        public WindowPositionDto()
        {
        }

        public WindowPositionDto(double left, double top, double width, double height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class IconDataDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IconPath { get; set; }
        public PointDto Position { get; set; }
        public SizeDto Size { get; set; }
    }

    public class PointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class SizeDto
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class WindowsMetadataDto
    {
        public List<string> WindowIds { get; set; }
    }

}
