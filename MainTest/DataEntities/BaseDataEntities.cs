using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//仅用于和Revit数据交互的数据结构
namespace MainTest
{
    public class Point3d
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3d(double x, double y, double z)
        {
            X = x; Y = y; Z = z;
        }
        public Point3d()
        {
        }

        public static Point3d Origin
        {
            get
            {
                return new Point3d(0, 0, 0);
            }
        }
    }

    public class Point2d
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2d(double x, double y)
        {
            X = x; Y = y;
        }

        public static Point2d Origin
        {
            get
            {
                return new Point2d(0, 0);
            }
        }
    }
    /// <summary>
    /// 全局 Double 转换器：所有 double 类型只保留 1 位小数
    /// </summary>
    public class DoubleRoundingConverter : JsonConverter<double>
    {
        int pres = 1;
        public DoubleRoundingConverter(int pres)
        {
            this.pres = pres;
        }
        public override void WriteJson(JsonWriter writer, double value, JsonSerializer serializer)
        {
            // 使用 Math.Round 保留 1 位小数
            // 注意：如果值是 5.0，JSON 中可能会直接写成 5，这在数值传输中是正常的
            writer.WriteValue(Math.Round(value, pres));
        }

        public override double ReadJson(JsonReader reader, Type objectType, double existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // 反序列化时，直接读取并转换为 double
            if (reader.TokenType == JsonToken.Null) return 0.0;
            return Convert.ToDouble(reader.Value);
        }
    }
    // Point3d 转换器：序列化为 [X, Y, Z]
    public class Point3dConverter : JsonConverter<Point3d>
    {
        public override void WriteJson(JsonWriter writer, Point3d value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(Math.Round(value.X, pres));
            writer.WriteValue(Math.Round(value.Y, pres));
            writer.WriteValue(Math.Round(value.Z, pres));
            writer.WriteEndArray();
        }

        public override Point3d ReadJson(JsonReader reader, Type objectType, Point3d existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return Point3d.Origin;
            var array = JArray.Load(reader);
            return new Point3d((double)array[0], (double)array[1], (double)array[2]);
        }
        int pres = 1;
        public Point3dConverter(int presi)
        {
            this.pres = presi;
        }
    }

    // Point2d 转换器：序列化为 [X, Y]
    public class Point2dConverter : JsonConverter<Point2d>
    {
        public override void WriteJson(JsonWriter writer, Point2d value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(Math.Round(value.X, pres));
            writer.WriteValue(Math.Round(value.Y, pres));
            writer.WriteEndArray();
        }

        public override Point2d ReadJson(JsonReader reader, Type objectType, Point2d existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return Point2d.Origin;
            var array = JArray.Load(reader);
            return new Point2d((double)array[0], (double)array[1]);
        }
        int pres = 1;
        public Point2dConverter(int presi)
        {
            this.pres = presi;
        }
    }

    // 提供一个全局通用的配置获取方法
    public static class CadJsonSettings
    {
        /// <summary>
        /// 带有Point3d、Point2d、Double精度的转换器
        /// </summary>
        /// <param name="pres">小数位数</param>
        /// <returns></returns>
        public static JsonSerializerSettings GetSettings(int pres = 1)
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new Point3dConverter(pres));
            settings.Converters.Add(new Point2dConverter(pres));
            // 2. 添加全局 Double 转换器 (保留1位小数)
            settings.Converters.Add(new DoubleRoundingConverter(pres));
            // 忽略空值，进一步缩小 JSON 体积
            settings.NullValueHandling = NullValueHandling.Ignore;
            return settings;
        }

        public static JsonSerializerSettings GetLineStringSettings(int pres = 1)
        {
            var settings = new JsonSerializerSettings();

            // 注册点和线相关转换器
            settings.Converters.Add(new Point3dConverter(pres));
            settings.Converters.Add(new Point2dConverter(pres));

            // 添加全局 Double 转换器 
            settings.Converters.Add(new DoubleRoundingConverter(pres));

            // 忽略空值，缩小 JSON 体积
            settings.NullValueHandling = NullValueHandling.Ignore;

            return settings;
        }
    }

    /// <summary>
    /// 风管数据json
    /// </summary>
    public class DuctDataForRevit
    {
        public Point2d basePoint;
        public List<BaseModelData> pipes = new List<BaseModelData>();
        public List<BaseElbowData> elbows = new List<BaseElbowData>();
        public List<BaseTeeData> tees = new List<BaseTeeData>();
        public List<BaseCrossData> crosses = new List<BaseCrossData>();
        public List<BaseTransitionData> reducers = new List<BaseTransitionData>();
        public List<BaseOffsetData> offsets = new List<BaseOffsetData>();

        public List<BaseAirTerminalData> airTeminals = new List<BaseAirTerminalData>();
        public List<BaseValveData> valves = new List<BaseValveData>();
        public List<BaseAirBlowerData> blowers = new List<BaseAirBlowerData>();
        public List<BaseAirBlowerData> equps = new List<BaseAirBlowerData>();

        public DuctDataForRevit() { }

        // 转化为 JSON 字符串
        public string ToJson()
        {
            // 使用 Formatting.Indented 可以让输出的 JSON 有缩进，方便调试和阅读
            return JsonConvert.SerializeObject(this, Formatting.Indented, CadJsonSettings.GetSettings());
        }

        // 从 JSON 字符串实例化对象（注意这里我改为了 static 方法，因为通常 FromJson 是用类名调用的）
        public static DuctDataForRevit FromJson(string dataStr)
        {
            if (string.IsNullOrWhiteSpace(dataStr)) return new DuctDataForRevit();
            return JsonConvert.DeserializeObject<DuctDataForRevit>(dataStr, CadJsonSettings.GetSettings());
        }
    }

    /// <summary>
    /// 桥架数据json
    /// </summary>
    public class CableTrayDataForRevit
    {
        public Point2d basePoint;
        public List<BaseModelData> pipes = new List<BaseModelData>();
        public List<BaseElbowData> elbows = new List<BaseElbowData>();
        public List<BaseTeeData> tees = new List<BaseTeeData>();
        public List<BaseCrossData> crosses = new List<BaseCrossData>();
        public List<BaseTransitionData> reducers = new List<BaseTransitionData>();
        public List<BaseOffsetData> offsets = new List<BaseOffsetData>();

        public List<BaseAirBlowerData> equps = new List<BaseAirBlowerData>();

        public CableTrayDataForRevit() { }

        // 转化为 JSON 字符串
        public string ToJson()
        {
            // 使用 Formatting.Indented 可以让输出的 JSON 有缩进，方便调试和阅读
            return JsonConvert.SerializeObject(this, Formatting.Indented, CadJsonSettings.GetSettings());
        }

        // 从 JSON 字符串实例化对象（注意这里我改为了 static 方法，因为通常 FromJson 是用类名调用的）
        public static CableTrayDataForRevit FromJson(string dataStr)
        {
            if (string.IsNullOrWhiteSpace(dataStr)) return new CableTrayDataForRevit();
            return JsonConvert.DeserializeObject<CableTrayDataForRevit>(dataStr, CadJsonSettings.GetSettings());
        }
    }
    /// <summary>
    /// 水数据json
    /// </summary>
    public class WaterPipeDataForRevit
    {
        public Point2d basePoint;
        public List<BaseWaterPipeData> waterPipes = new List<BaseWaterPipeData>();
        public List<BaseValveData> waterVaves = new List<BaseValveData>();
        /// <summary>
        /// 这里用风机数据结构代替水管设备
        /// </summary>
        public List<BaseAirBlowerData> waterEquips = new List<BaseAirBlowerData>();
        /// <summary>
        /// 喷淋系统喷头
        /// </summary>
        public List<BaseAirBlowerData> fireSprinkler = new List<BaseAirBlowerData>();

        public WaterPipeDataForRevit() { }

        // 转化为 JSON 字符串
        public string ToJson()
        {
            // 使用 Formatting.Indented 可以让输出的 JSON 有缩进，方便调试和阅读
            return JsonConvert.SerializeObject(this, Formatting.Indented, CadJsonSettings.GetSettings());
        }

        // 从 JSON 字符串实例化对象（注意这里我改为了 static 方法，因为通常 FromJson 是用类名调用的）
        public static WaterPipeDataForRevit FromJson(string dataStr)
        {
            if (string.IsNullOrWhiteSpace(dataStr)) return new WaterPipeDataForRevit();
            return JsonConvert.DeserializeObject<WaterPipeDataForRevit>(dataStr, CadJsonSettings.GetSettings());
        }
    }

    public class BaseModelData
    {
        /// <summary>
        /// 子系统,用于标记连接关系
        /// </summary>
        public string SubSystem { get; set; }
        /// <summary>
        /// 系统，在风管中表示排烟排风等系统，在水管中表示排水，自来水，废水等系统
        /// </summary>
        public string System { get; set; }
        /// <summary>
        /// 在拓扑图结构中，作为id使用
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        /// 层名
        /// </summary>
        public string LayerName { get; set; }

        /// <summary>
        /// 起点连接的管件Number
        /// </summary>
        public string StartConnectNumber { get; set; }
        /// <summary>
        /// 终点连接的管件Number
        /// </summary>
        public string EndConnectNumber { get; set; }

        /// <summary>
        /// 样式
        /// </summary>
        public string Style { get; set; }

        private double mStartWidth;
        private double mStartThickness;
        private double mEndWidth;
        private double mEndThickness;

        /// <summary>
        /// 始端厚度
        /// </summary>
        public double StartThickness
        {
            get
            {
                return mStartThickness;
            }
            set
            {
                mStartThickness = value;

            }
        }
        /// <summary>
        /// 始端宽度（圆形管时为直径）
        /// </summary>
        public double StartWidth
        {
            get
            {
                return mStartWidth;
            }
            set
            {
                mStartWidth = value;

            }
        }

        /// <summary>
        /// 末端厚度
        /// </summary>
        public double EndThickness
        {
            get
            {
                return mEndThickness;
            }
            set
            {
                mEndThickness = value;
            }
        }

        /// <summary>
        /// 末端宽度（圆形管时为直径）
        /// </summary>
        public double EndWidth
        {
            get
            {
                return mEndWidth;
            }

            set
            {
                mEndWidth = value;

            }
        }
        /// <summary>
        /// 起点坐标
        /// </summary>
        public Point3d StartPosition { get; set; }
        /// <summary>
        /// 终点坐标
        /// </summary>
        public Point3d EndPosition { get; set; }

        //增加属性是否是统层立管（引线标注的立管） 20260428
        public bool IsWholeFloorVerPipe
        {
            get; set;
        } = false;


        //sb.AppendLine(",,,支管位置坐标,支管厚度,支管宽度,曲率半径,,,,,,,,,,支管连接编号");
        //sb.AppendLine(",,,曲率半径,,,,,,,角度,,交点坐标,,");
        //sb.AppendLine(",,,,,,,,,角度,,,,");
        //sb.AppendLine(",,,,,,,,,,长度,,,");
        //sb.AppendLine(",,,,,,,,,,");

        //sb.AppendLine(",,坡度,,,流动方向,管径,,,,");

        //sb.AppendLine(",,名称,坐标,型号,宽度,旋转角度,,长度,风量,,,");
        //sb.AppendLine(",,名称,坐标,型号,宽度,旋转角度,直径,,长度,,,");
        //sb.AppendLine(",,,支管1位置坐标,支管1厚度,支管1宽度,支管2位置坐标,支管2厚度,支管2宽度,
        //,,,,,,,,,,支管1连接编号,支管2连接编号");

        //sb.AppendLine(",,名称,坐标,型号,,旋转角度,,长度,,,");

    }
    /// <summary>
    /// 弯头数据
    /// </summary>
    public class BaseElbowData : BaseModelData
    {
        /// <summary>
        /// 曲率半径
        /// </summary>
        public double Curvature { get; set; }
        /// <summary>
        /// 交点位置，为弯头端线的垂直平分线交点，也就是连接的风管的延长线交点
        /// </summary>
        public Point3d CornerPosition = new Point3d();
        /// <summary>
        /// 角度
        /// </summary>
        public double Angle = 0;

    }

    /// <summary>
    /// 三通数据
    /// </summary>
    public class BaseTeeData : BaseModelData
    {
        /// <summary>
        /// 支管坐标
        /// </summary>
        public Point3d BranchPosition { get; set; }
        /// <summary>
        /// 支管厚度
        /// </summary>
        public double BranchThickness { get; set; }
        /// <summary>
        /// 支管宽度（圆形管时为直径）
        /// </summary>
        public double BranchWidth { get; set; }
        /// <summary>
        /// 曲率半径
        /// </summary>
        public double Curvature { get; set; }
        /// <summary>
        /// 支管连接的管件Number
        /// </summary>
        public string BranchConnectNumber { get; set; }
    }

    /// <summary>
    /// 四通数据
    /// </summary>
    public class BaseCrossData : BaseModelData
    {
        /// <summary>
        /// 支管1坐标
        /// </summary>
        public Point3d Branch1Position { get; set; }
        /// <summary>
        /// 支管2坐标
        /// </summary>
        public Point3d Branch2Position { get; set; }
        /// <summary>
        /// 支管1厚度
        /// </summary>
        public double Branch1Thickness { get; set; }
        /// <summary>
        /// 支管2厚度
        /// </summary>
        public double Branch2Thickness { get; set; }
        /// <summary>
        /// 支管1宽度（圆形管时为直径）
        /// </summary>
        public double Branch1Width { get; set; }
        /// <summary>
        /// 支管2宽度（圆形管时为直径）
        /// </summary>
        public double Branch2Width { get; set; }

        /// <summary>
        /// 支管1连接的管件Number
        /// </summary>
        public string Branch1ConnectNumber { get; set; }
        /// <summary>
        /// 支管2连接的管件Number
        /// </summary>
        public string Branch2ConnectNumber { get; set; }
    }

    /// <summary>
    /// 变径
    /// </summary>
    public class BaseTransitionData : BaseModelData
    {
        /// <summary>
        /// 长度
        /// </summary>
        public double Length { get; set; }

    }

    /// <summary>
    /// 乙字弯
    /// </summary>
    public class BaseOffsetData : BaseModelData
    {
        /// <summary>
        /// 长度
        /// </summary>
        public double Length { get; set; }

    }
    /// <summary>
    /// 风口
    /// </summary>
    public class BaseAirTerminalData : BaseModelData
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 位置
        /// </summary>
        public Point3d Location { get; set; }

        /// <summary>
        /// 旋转角度
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 风量
        /// </summary>
        public double Airflow { get; set; }

    }

    /// <summary>
    /// 阀门数据
    /// </summary>
    public class BaseValveData : BaseModelData
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        public double Diameter { get; set; }
        /// <summary>
        /// 位置
        /// </summary>
        public Point3d Location { get; set; }
        /// <summary>
        /// 旋转角度
        /// </summary>
        public double Angle { get; set; }
    }
    /// <summary>
    /// 风机或设备共用
    /// </summary>
    public class BaseAirBlowerData : BaseModelData
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 位置
        /// </summary>
        public Point3d Location { get; set; }
        /// <summary>
        /// 旋转角度
        /// </summary>
        public double Angle { get; set; }
    }
    /// <summary>
    /// 水管
    /// </summary>
    public class BaseWaterPipeData : BaseModelData
    {
        public double Diameter { get; set; }
        /// <summary>
        /// 坡度
        /// </summary>
        public double Slope { get; set; }
        /// <summary>
        /// 流动方向
        /// </summary>
        public string Direction { get; set; }
        /// <summary>
        /// 水管特别设置的连接关系id，
        /// 不使用基类的StartConnectNumber和EndConnectNumber
        /// </summary>
        public List<string> ConnectNumbers = new List<string>();
    }
}
