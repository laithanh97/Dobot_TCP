using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Dobot_TCP.com.dobot.api
{
    class ErrorInfoHelper
    {
        private readonly static Dictionary<int, ErrorInfoBean> mControllerBeans = new Dictionary<int, ErrorInfoBean>();
        private readonly static Dictionary<int, ErrorInfoBean> mServoBeans = new Dictionary<int, ErrorInfoBean>();
        public static void ParseControllerJsonFile(string strFullFile)
        {
            try
            {
                string strJson = System.IO.File.ReadAllText(strFullFile);
                List<ErrorInfoBean> result = JsonConvert.DeserializeObject<List<ErrorInfoBean>>(strJson);
                foreach (var bean in result)
                {
                    bean.Type = "Controller";
                    mControllerBeans.Add(bean.id, bean);
                }
            }
            catch (Exception)
            {
            }
        }
        public static void ParseServoJsonFile(string strFullFile)
        {
            try
            {
                string strJson = System.IO.File.ReadAllText(strFullFile);
                List<ErrorInfoBean> result = JsonConvert.DeserializeObject<List<ErrorInfoBean>>(strJson);
                foreach (var bean in result)
                {
                    bean.Type = "Servo";
                    mServoBeans.Add(bean.id, bean);
                }
            }
            catch (Exception)
            {
            }
        }

        public static ErrorInfoBean FindController(int id)
        {
            return mControllerBeans.ContainsKey(id) ? mControllerBeans[id] : null;
        }
        public static ErrorInfoBean FindServo(int id)
        {
            return mServoBeans.ContainsKey(id) ? mServoBeans[id] : null;
        }
    }
}
