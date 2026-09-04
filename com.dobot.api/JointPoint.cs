using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dobot_TCP.com.dobot.api
{
    class JointPoint
    {
        /// <summary>
        /// J1 轴位置，单位：度
        /// </summary>
        public double j1 { get; set; }

        /// <summary>
        /// J2 轴位置，单位：度
        /// </summary>
        public double j2 { get; set; }

        /// <summary>
        /// J3 轴位置，单位：度
        /// </summary>
        public double j3 { get; set; }

        /// <summary>
        /// J4 轴位置，单位：度
        /// </summary>
        public double j4 { get; set; }

        public JointPoint()
        {
            j1 = j2 = j3 = j4 = 0.0;
        }

        override public string ToString()
        {
            string str = $"{this.j1},{this.j2},{this.j3},{this.j4}";
            return str;
        }
    }
}
