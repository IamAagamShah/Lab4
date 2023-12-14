using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AWSserverlessApk
{
    internal class ImageLabelItem
    {
        public string ImageID { get; set; } // Primary Key
        public string ImageURL { get; set; }
        public Dictionary<string, float> Labels { get; set; }
    }
}
