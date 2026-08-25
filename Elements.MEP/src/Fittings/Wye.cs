using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Flow;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Newtonsoft.Json;

namespace Elements.Fittings
{
    /// <summary>
    /// Y型三通（Wye）几何参数及规则配置项
    /// </summary>
    public class WyeSettings
    {
        /// <summary>
        /// 直通分支端口距离中心的偏移距离
        /// </summary>
        public double MainDistance;

        /// <summary>
        /// 侧向分支端口距离中心的偏移距离
        /// </summary>
        public double BranchDistance;

        /// <summary>
        /// 主干端口距离中心的偏移距离
        /// </summary>
        public double TrunkDistance;

        /// <summary>
        /// 直通分支的圆形直径
        /// </summary>
        public double MainDiameter;

        /// <summary>
        /// 侧向分支的圆形直径
        /// </summary>
        public double BranchDiameter;

        /// <summary>
        /// 主干的圆形直径
        /// </summary>
        public double Diameter;

        /// <summary>
        /// 直通分支端口宽度
        /// </summary>
        public double MainWidth;

        /// <summary>
        /// 直通分支端口高度
        /// </summary>
        public double MainHeight;

        /// <summary>
        /// 侧向分支端口宽度
        /// </summary>
        public double BranchWidth;

        /// <summary>
        /// 侧向分支端口高度
        /// </summary>
        public double BranchHeight;

        /// <summary>
        /// 主干端口宽度
        /// </summary>
        public double Width;

        /// <summary>
        /// 主干端口高度
        /// </summary>
        public double Height;

        /// <summary>
        /// 端口截面形状类型（圆形、矩形等）
        /// </summary>
        public ShapeType ShapeType;

        /// <summary>
        /// 侧向分支角度校验容差
        /// </summary>
        public double AngleTolerance;

        /// <summary>
        /// 端口定位/连接匹配距离容差
        /// </summary>
        public double PortsDistanceTolerance;

        /// <summary>
        /// 允许的侧向分支夹角数组（单位：度）
        /// </summary>
        public double[] AllowedBranchAngles = new[] { 45.0, 90.0, 180.0 };

        /// <summary>
        /// 默认构造函数（使用默认管径与圆形截面）
        /// </summary>
        public WyeSettings()
        {
            this.TrunkDistance = 0.06;
            this.MainDistance = 0.1;
            this.BranchDistance = 0.1;
            this.MainDiameter = FittingTreeRouting.DefaultDiameter;
            this.BranchDiameter = FittingTreeRouting.DefaultDiameter;
            this.Diameter = FittingTreeRouting.DefaultDiameter;
            this.MainWidth = this.MainDiameter;
            this.MainHeight = this.MainDiameter;
            this.BranchWidth = this.BranchDiameter;
            this.BranchHeight = this.BranchDiameter;
            this.Width = this.Diameter;
            this.Height = this.Diameter;
            this.ShapeType = ShapeType.Circle;
            this.AngleTolerance = 0.1;
            this.PortsDistanceTolerance = 0.001;
        }

        /// <summary>
        /// 基于管径（圆形截面）的 WyeSettings 构造函数
        /// </summary>
        public WyeSettings(double trunkDiameter,
            double mainDiameter,
            double branchDiameter,
            double trunkDistance,
            double mainDistance,
            double branchDistance,
            double[] allowedAngles = null,
            double angleTolerance = 0.1,
            double portsDistanceTolerance = 0.001)
        {
            this.Diameter = trunkDiameter;
            this.MainDiameter = mainDiameter;
            this.BranchDiameter = branchDiameter;
            this.Width = this.Height = trunkDiameter;
            this.MainWidth = this.MainHeight = mainDiameter;
            this.BranchWidth = this.BranchHeight = branchDiameter;
            this.ShapeType = ShapeType.Circle;

            this.TrunkDistance = trunkDistance;
            this.MainDistance = mainDistance;
            this.BranchDistance = branchDistance;

            if (allowedAngles != null)
            {
                this.AllowedBranchAngles = allowedAngles;
            }

            this.AngleTolerance = angleTolerance;
            this.PortsDistanceTolerance = portsDistanceTolerance;
        }

        /// <summary>
        /// 支持显式设置主干、直通分支、侧向分支各自宽和高（适合矩形风管/异形截面管件）的构造函数
        /// </summary>
        /// <param name="trunkWidth">主干宽度</param>
        /// <param name="trunkHeight">主干高度</param>
        /// <param name="mainWidth">直通分支宽度</param>
        /// <param name="mainHeight">直通分支高度</param>
        /// <param name="branchWidth">侧向分支宽度</param>
        /// <param name="branchHeight">侧向分支高度</param>
        /// <param name="trunkDistance">主干延伸距离</param>
        /// <param name="mainDistance">直通分支延伸距离</param>
        /// <param name="branchDistance">侧向分支延伸距离</param>
        /// <param name="shapeType">截面形状（默认为矩形 Rectangular）</param>
        /// <param name="allowedAngles">允许的侧向分支角度列表</param>
        /// <param name="angleTolerance">角度判断容差</param>
        /// <param name="portsDistanceTolerance">端口定位距离容差</param>
        public WyeSettings(
            double trunkWidth,
            double trunkHeight,
            double mainWidth,
            double mainHeight,
            double branchWidth,
            double branchHeight,
            double trunkDistance,
            double mainDistance,
            double branchDistance,
            ShapeType shapeType = ShapeType.Rectangle,
            double[] allowedAngles = null,
            double angleTolerance = 0.1,
            double portsDistanceTolerance = 0.001)
        {
            this.Width = trunkWidth;
            this.Height = trunkHeight;
            this.MainWidth = mainWidth;
            this.MainHeight = mainHeight;
            this.BranchWidth = branchWidth;
            this.BranchHeight = branchHeight;

            // 给 Diameter 打底（取最大边长），防止后续读取管径作为兜底计算时出现 0
            this.Diameter = Math.Max(trunkWidth, trunkHeight);
            this.MainDiameter = Math.Max(mainWidth, mainHeight);
            this.BranchDiameter = Math.Max(branchWidth, branchHeight);

            this.ShapeType = shapeType;

            this.TrunkDistance = trunkDistance;
            this.MainDistance = mainDistance;
            this.BranchDistance = branchDistance;

            if (allowedAngles != null)
            {
                this.AllowedBranchAngles = allowedAngles;
            }

            this.AngleTolerance = angleTolerance;
            this.PortsDistanceTolerance = portsDistanceTolerance;
        }
    }
    public partial class Wye
    {
        [JsonProperty]
        public double AngleTolerance { get; set; }

        [JsonProperty]
        public double PositionTolerance { get; set; }

        public Wye(Vector3 position, Vector3 mainDirection, Vector3 branchDirection, WyeSettings wyeSettings, Material material) : this(position,
                                                                                                                                 mainDirection.Negate(),
                                                                                                                                 mainDirection,
                                                                                                                                 branchDirection,
                                                                                                                                 wyeSettings,
                                                                                                                                 material)
        { }

        public Wye(Vector3 position, Vector3 trunkDirection, Vector3 mainDirection, Vector3 branchDirection, WyeSettings wyes, Material material) :
                                                                                         base(false, FittingLocator.Empty(), new Transform(),
                                                                                              material == null ? FittingTreeRouting.DefaultFittingMaterial : material,
                                                                                              new Representation(new List<SolidOperation>()),
                                                                                              false,
                                                                                              Guid.NewGuid(),
                                                                                              "")
        {
            this.Transform = new Transform(position);

            this.Trunk = CreatePort(position + trunkDirection.Unitized() * wyes.TrunkDistance,
                                    trunkDirection.Unitized(),
                                    wyes.Diameter,
                                    wyes.Width,
                                    wyes.Height,
                                    wyes.ShapeType);
            this.MainBranch = CreatePort(position + mainDirection.Unitized() * wyes.MainDistance,
                                         mainDirection.Unitized(),
                                         wyes.MainDiameter,
                                         wyes.MainWidth,
                                         wyes.MainHeight,
                                         wyes.ShapeType);

            var branchAngle = branchDirection.AngleTo(mainDirection);
            if (wyes.AllowedBranchAngles.Count() > 0 && wyes.AllowedBranchAngles.All(a => !branchAngle.ApproximatelyEquals(a, wyes.AngleTolerance)))
            {
                throw new ArgumentOutOfRangeException($"That branch directions provided make an angle of {branchAngle} which is not allowed for this wyes settings");
            }

            var branchEnd = position + branchDirection.Unitized() * wyes.BranchDistance;

            this.SideBranch = CreatePort(branchEnd,
                                         branchDirection,
                                         wyes.BranchDiameter,
                                         wyes.BranchWidth,
                                         wyes.BranchHeight,
                                         wyes.ShapeType);

            AngleTolerance = wyes.AngleTolerance;
            PositionTolerance = wyes.PortsDistanceTolerance;
        }

        private static Port CreatePort(Vector3 position,
                                       Vector3 direction,
                                       double diameter,
                                       double width,
                                       double height,
                                       ShapeType shapeType)
        {
            if (shapeType == ShapeType.Circle)
            {
                return new Port(position, direction, diameter);
            }

            return new Port(position,
                            direction,
                            width > 0 ? width : diameter,
                            height > 0 ? height : diameter,
                            shapeType);
        }

        public static (Connection mainConnection, Connection branchConnection) GetMainAndBranch(Connection[] connections, Connection outgoing)
        {
            var firstAngle = connections[0].Direction().AngleTo(outgoing.Direction());
            var secondAngle = connections[1].Direction().AngleTo(outgoing.Direction());
            if (firstAngle.ApproximatelyEquals(0, 1))
            {
                return (connections[0], connections[1]);
            }
            else if (secondAngle.ApproximatelyEquals(0, 1))
            {
                return (connections[1], connections[0]);
            }
            else
            {
                // fairly robust fallback if neither incoming branch is aligned with the outgoing branch
                if (firstAngle % 0 > secondAngle % 90)
                {
                    return (connections[0], connections[1]);
                }
                else
                {
                    return (connections[1], connections[0]);
                }
            }
        }

        public override void UpdateRepresentations()
        {
            var trunkPosition = Trunk.Position;
            var mainPosition = MainBranch.Position;
            var branchPosition = SideBranch.Position;
            var origin = Transform.Origin;

            var trunkProfile = PipeProfile.Create(Trunk);
            var trunkLine = new Line(Vector3.Origin, trunkPosition - origin);
            if (UseRepresentationInstances)
            {
                trunkLine = trunkLine.TransformedLine(GetRotatedTransform().Inverted());
            }
            var trunk = new Sweep(trunkProfile, trunkLine, 0, 0, 0, false);

            var mainProfile = PipeProfile.Create(MainBranch);
            var mainLine = new Line(Vector3.Origin, mainPosition - origin);
            if (UseRepresentationInstances)
            {
                mainLine = mainLine.TransformedLine(GetRotatedTransform().Inverted());
            }
            var main = new Sweep(mainProfile, mainLine, 0, 0, 0, false);

            var branchProfile = PipeProfile.Create(SideBranch);
            var branchLine = new Line(Vector3.Origin, branchPosition - origin);
            if (UseRepresentationInstances)
            {
                branchLine = branchLine.TransformedLine(GetRotatedTransform().Inverted());
            }
            var branch = new Sweep(branchProfile, branchLine, 0, 0, 0, false);

            var arrows = new List<SolidOperation>();
            arrows.AddRange(Trunk.GetArrow(Transform.Origin, fittingRotationTransform: GetRotatedTransform()));
            arrows.AddRange(SideBranch.GetArrow(Transform.Origin, fittingRotationTransform: GetRotatedTransform()));
            arrows.AddRange(MainBranch.GetArrow(Transform.Origin, fittingRotationTransform: GetRotatedTransform()));
            var solidOps = new List<SolidOperation> { trunk, main, branch }.Concat(arrows).Concat(GetExtensions()).ToList();
            if (UseRepresentationInstances)
            {
                FittingRepresentationStorage.SetFittingRepresentation(this, () => solidOps);
            }
            else
            {
                Representation = new Geometry.Representation(solidOps);
            }
        }

        public override Port[] GetPorts()
        {
            return new[] { this.Trunk, this.MainBranch, this.SideBranch };
        }

        public override List<Port> BranchSidePorts()
        {
            return new List<Port> { MainBranch, SideBranch };
        }

        public override Port TrunkSidePort()
        {
            return Trunk;
        }

        public ComponentBase GetBranchSideComponent(Port connector)
        {
            if (connector != MainBranch && connector != SideBranch)
            {
                return null;
            }

            return BranchSideComponents.SingleOrDefault(x =>
            {
                if (x is StraightSegment)
                {
                    return x.TrunkSidePort().IsIdenticalConnector(connector, PositionTolerance, AngleTolerance);
                }

                return x.TrunkSidePort().IsComplimentaryConnector(connector, PositionTolerance, AngleTolerance);
            });
        }

        public override Transform GetRotatedTransform()
        {
            var zAxis = Trunk.Direction.Cross(SideBranch.Direction).Unitized();
            var t = new Transform(Vector3.Origin, Trunk.Direction, zAxis);
            return t;
        }

        /// <inheritdoc/>
        public override string GetRepresentationHash()
        {
            var props = new object[] {
                Trunk.ShapeType,
                Trunk.Width,
                Trunk.Height,
                (Trunk.Position - Transform.Origin).LengthSquared(),
                MainBranch.ShapeType,
                MainBranch.Width,
                MainBranch.Height,
                (MainBranch.Position - Transform.Origin).LengthSquared(),
                SideBranch.ShapeType,
                SideBranch.Width,
                SideBranch.Height,
                (SideBranch.Position - Transform.Origin).LengthSquared(),
                Angle
            };
            return $"{this.GetType().Name}-{String.Join("-", props.Select(p => p.ToString()))}";
        }
    }
}
