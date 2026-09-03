using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Elements;
using Elements.Fittings;
using Elements.Geometry;
using Elements.Serialization.glTF;

namespace Elements.Pipeline.Core.Import;

/// <summary>
/// Converts MainTest.DataEntities.DuctDataForRevit data to Elements fittings.
/// The Revit export stores coordinates and dimensions in millimeters.
/// </summary>
public static class RevitDuctJsonImporter
{
    private const double PointTolerance = 0.000001;
    private const double MinimumDimension = 0.001;
    private const double MaximumSectionDimension = 10.0;
    private const double DefaultSectionDimension = 0.1;

    public static string ImportJsonToGltf(string jsonPath,
                                          double unitScale = 0.001,
                                          ShapeType? sectionShape = null)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            throw new ArgumentException("A DuctDataForRevit JSON file path is required.", nameof(jsonPath));
        }

        var data = DuctDataForRevit.FromJson(File.ReadAllText(jsonPath));
        var model = CreateModel(data, unitScale, sectionShape);
        var outputPath = Path.ChangeExtension(Path.GetFullPath(jsonPath), ".gltf");
        model.ToGlTF(outputPath, false);
        return outputPath;
    }

    public static Model CreateModel(DuctDataForRevit data,
                                    double unitScale = 0.001,
                                    ShapeType? sectionShape = null)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        if (unitScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitScale), "Unit scale must be greater than zero.");
        }

        ComponentBase.UseRepresentationInstances = false;
        Port.ShowArrows = false;
        Console.WriteLine($"Pipe:{data.pipes.Count}__El: {data.elbows.Count}__Tee: {data.tees.Count}__Cr:{data.crosses.Count}__Re: {data.reducers.Count}");
        var lookup = BuildLookup(data);
        var offset = GetModelOffset(data, unitScale);
        var model = new Model();

        foreach (var pipe in data.pipes ?? new List<BaseModelData>())
        {
            Add(model, CreateStraight(pipe, lookup, unitScale, offset, ResolveShapeByStyle(pipe.Style, sectionShape)), pipe, "Pipe");
        }
        foreach (var elbow in data.elbows ?? new List<BaseElbowData>())
        {
            Add(model, CreateElbow(elbow, lookup, unitScale, offset, ResolveShapeByStyle(elbow.Style, sectionShape)), elbow, "Elbow");
        }
        foreach (var tee in data.tees ?? new List<BaseTeeData>())
        {
            Add(model, CreateTee(tee, lookup, unitScale, offset, ResolveShapeByStyle(tee.Style, sectionShape)), tee, "Tee");
        }
        foreach (var cross in data.crosses ?? new List<BaseCrossData>())
        {
            Add(model, CreateCross(cross, lookup, unitScale, offset, ResolveShapeByStyle(cross.Style, sectionShape)), cross, "Cross");
        }
        foreach (var reducer in data.reducers ?? new List<BaseTransitionData>())
        {
            Add(model, CreateReducer(reducer, lookup, unitScale, offset, ResolveShapeByStyle(reducer.Style, sectionShape)), reducer, "Reducer");
        }

        return model;
    }

    public static string Run(string? jsonPath = null,
                             double unitScale = 0.001,
                             ShapeType? sectionShape = null)
    {
        if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            return ImportJsonToGltf(jsonPath, unitScale, sectionShape);
        }

        var model = CreateModel(CreateSampleData(), 1.0);
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "duct-data-for-revit-example.gltf");
        model.ToGlTF(outputPath, false);
        return outputPath;
    }

    private static void Add(Model model, Element element, BaseModelData source, string sourceType)
    {
        element.Id = CreateStableId(source, sourceType);
        element.Name = string.IsNullOrWhiteSpace(source.Number) ? element.GetType().Name : source.Number;
        model.AddElement(element);
    }

    private static Guid CreateStableId(BaseModelData source, string sourceType)
    {
        var key = string.Join("|",
                              sourceType,
                              source.System ?? string.Empty,
                              source.SubSystem ?? string.Empty,
                              source.Number ?? string.Empty,
                              PointKey(source.StartPosition),
                              PointKey(source.EndPosition));
        using var md5 = MD5.Create();
        return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(key)));
    }

    private static string PointKey(Point3d point)
    {
        return string.Join(",",
                           point.X.ToString("R", CultureInfo.InvariantCulture),
                           point.Y.ToString("R", CultureInfo.InvariantCulture),
                           point.Z.ToString("R", CultureInfo.InvariantCulture));
    }

    private static StraightSegment CreateStraight(BaseModelData data,
                                                  IReadOnlyDictionary<string, BaseModelData> lookup,
                                                  double scale,
                                                  Vector3 offset,
                                                  ShapeType shape)
    {
        var start = ToVector(data.StartPosition, scale, offset);
        var end = ToVector(data.EndPosition, scale, offset);
        var axis = Direction(start, end, "pipe");
        var startDimensions = DimensionsAt(data, true, lookup, scale, shape);
        var endDimensions = DimensionsAt(data, false, lookup, scale, shape);
        var startPort = MakePort(start, axis.Negate(), startDimensions);
        var endPort = MakePort(end, axis, endDimensions);
        return new StraightSegment(0, endPort, startPort, allowMismatch: true);
    }

    private static Elbow CreateElbow(BaseElbowData data,
                                     IReadOnlyDictionary<string, BaseModelData> lookup,
                                     double scale,
                                     Vector3 offset,
                                     ShapeType shape)
    {
        var start = ToVector(data.StartPosition, scale, offset);
        var end = ToVector(data.EndPosition, scale, offset);
        var corner = IsValidCorner(data.CornerPosition, data.StartPosition, data.EndPosition)
            ? ToVector(data.CornerPosition, scale, offset)
            : ReconstructElbowCorner(data, lookup, start, end, scale);
        var startDirection = Direction(corner, start, "elbow start");
        var endDirection = Direction(corner, end, "elbow end");
        var dimensions = DimensionsAt(data, true, lookup, scale, shape);
        var radius = Math.Max(0, data.Curvature * scale);
        var sideLength = Math.Min(corner.DistanceTo(start), corner.DistanceTo(end)) - radius;
        if (sideLength < -PointTolerance)
        {
            throw new InvalidDataException($"Elbow {data.Number} curvature is larger than its endpoint distance.");
        }

        return new Elbow(corner, startDirection, endDirection, Math.Max(0, sideLength),
                         dimensions.width, dimensions.height, dimensions.shape,
                         FittingTreeRouting.DefaultFittingMaterial, radius);
    }

    private static Wye CreateTee(BaseTeeData data,
                                 IReadOnlyDictionary<string, BaseModelData> lookup,
                                 double scale,
                                 Vector3 offset,
                                 ShapeType shape)
    {
        var start = ToVector(data.StartPosition, scale, offset);
        var end = ToVector(data.EndPosition, scale, offset);
        var branch = ToVector(data.BranchPosition, scale, offset);
        var center = ClosestPointOnLine(start, end, branch);
        var trunk = Direction(center, start, "tee trunk");
        var main = Direction(center, end, "tee main");
        var side = Direction(center, branch, "tee branch");
        var trunkDimensions = DimensionsAt(data, true, lookup, scale, shape);
        var mainDimensions = DimensionsAt(data, false, lookup, scale, shape);
        var branchDimensions = BranchDimensions(data, lookup, scale, shape);
        var settings = new WyeSettings(trunkDimensions.width, trunkDimensions.height,
                                       mainDimensions.width, mainDimensions.height,
                                       branchDimensions.width, branchDimensions.height,
                                       center.DistanceTo(start), center.DistanceTo(end), center.DistanceTo(branch),
                                       trunkDimensions.shape,
                                       new[] { 26.0, 45.0, 90.0, 135.0, 154.0 });
        return new Wye(center, trunk, main, side, settings, FittingTreeRouting.DefaultFittingMaterial);
    }

    private static Cross CreateCross(BaseCrossData data,
                                     IReadOnlyDictionary<string, BaseModelData> lookup,
                                     double scale,
                                     Vector3 offset,
                                     ShapeType shape)
    {
        var start = ToVector(data.StartPosition, scale, offset);
        var end = ToVector(data.EndPosition, scale, offset);
        var branch1 = ToVector(data.Branch1Position, scale, offset);
        var branch2 = ToVector(data.Branch2Position, scale, offset);
        var center = ClosestPointOnLine(start, end, (branch1 + branch2) / 2.0);
        var trunk = Direction(center, start, "cross trunk");
        var main = Direction(center, end, "cross main");
        var side1 = Direction(center, branch1, "cross branch 1");
        var side2 = Direction(center, branch2, "cross branch 2");
        var trunkDimensions = DimensionsAt(data, true, lookup, scale, shape);
        var mainDimensions = DimensionsAt(data, false, lookup, scale, shape);
        var branch1Dimensions = BranchDimensions(data, true, lookup, scale, shape);
        var branch2Dimensions = BranchDimensions(data, false, lookup, scale, shape);
        var settings = new CrossSettings
        {
            Distance_Trunk = center.DistanceTo(start),
            Distance_A = center.DistanceTo(end),
            Distance_B = center.DistanceTo(branch1),
            Distance_C = center.DistanceTo(branch2),
            Diameter = Diameter(trunkDimensions),
            Diameter_A = Diameter(mainDimensions),
            Diameter_B = Diameter(branch1Dimensions),
            Diameter_C = Diameter(branch2Dimensions),
            Width = trunkDimensions.width,
            Height = trunkDimensions.height,
            Width_A = mainDimensions.width,
            Height_A = mainDimensions.height,
            Width_B = branch1Dimensions.width,
            Height_B = branch1Dimensions.height,
            Width_C = branch2Dimensions.width,
            Height_C = branch2Dimensions.height,
            ShapeType = trunkDimensions.shape,
            AllowedBranchAngles = new[] { 90.0 }
        };
        return new Cross(center, trunk, main, side1, side2, settings, FittingTreeRouting.DefaultFittingMaterial);
    }

    private static Reducer CreateReducer(BaseTransitionData data,
                                         IReadOnlyDictionary<string, BaseModelData> lookup,
                                         double scale,
                                         Vector3 offset,
                                         ShapeType shape)
    {
        var start = ToVector(data.StartPosition, scale, offset);
        var end = ToVector(data.EndPosition, scale, offset);
        var directionToStart = Direction(end, start, "reducer start");
        var center = (start + end) / 2.0;
        var startDimensions = DimensionsAt(data, true, lookup, scale, shape);
        var endDimensions = DimensionsAt(data, false, lookup, scale, shape);
        var length = data.Length > 0 ? data.Length * scale : start.DistanceTo(end);
        return new Reducer(center, directionToStart,
                           endDimensions.width, endDimensions.height, endDimensions.shape,
                           startDimensions.width, startDimensions.height, startDimensions.shape,
                           length, FittingTreeRouting.DefaultFittingMaterial);
    }

    private static (double width, double height, ShapeType shape) DimensionsAt(BaseModelData data,
                                                                                 bool start,
                                                                                 IReadOnlyDictionary<string, BaseModelData> lookup,
                                                                                 double scale,
                                                                                 ShapeType defaultShape)
    {
        var width = start ? data.StartWidth : data.EndWidth;
        var height = start ? data.StartThickness : data.EndThickness;
        var connection = start ? data.StartConnectNumber : data.EndConnectNumber;
        var endpoint = start ? data.StartPosition : data.EndPosition;
        var style = NormalizeStyle(data.Style);
        var isTransition = style.Contains("\u5929\u5706\u5730\u65b9", StringComparison.OrdinalIgnoreCase);
        var endpointShape = style == "天圆地方"
            ? InferTransitionShape(width, height, connection, endpoint, lookup)
            : defaultShape;
        if (isTransition)
        {
            endpointShape = InferTransitionShape(width, height, connection, endpoint, lookup);
        }

        var connected = GetConnectedEndpoint(connection, endpoint, lookup);
        if (!Positive(width) && Positive(connected.width)) width = connected.width;
        if (!Positive(height) && Positive(connected.height)) height = connected.height;

        if (endpointShape == ShapeType.Circle)
        {
            var diameter = Positive(width) ? width : height;
            if (!Positive(diameter))
            {
                diameter = connected.width > 0 ? connected.width : connected.height;
            }
            diameter = SanitizeDimension(diameter * scale, DefaultSectionDimension);
            return (diameter, diameter, ShapeType.Circle);
        }

        var widthInMeters = width * scale;
        var heightInMeters = height * scale;
        widthInMeters = SanitizeDimension(widthInMeters,
                                          Positive(heightInMeters) ? heightInMeters : DefaultSectionDimension);
        heightInMeters = SanitizeDimension(heightInMeters, widthInMeters);
        return (widthInMeters, heightInMeters, endpointShape);
    }

    private static (double width, double height, ShapeType shape) BranchDimensions(BaseTeeData data,
                                                                                     IReadOnlyDictionary<string, BaseModelData> lookup,
                                                                                     double scale,
                                                                                     ShapeType shape)
    {
        var branch = new BaseModelData
        {
            Style = data.Style,
            StartWidth = data.BranchWidth,
            StartThickness = data.BranchThickness,
            StartConnectNumber = data.BranchConnectNumber,
            StartPosition = data.BranchPosition
        };
        return DimensionsAt(branch, true, lookup, scale, shape);
    }

    private static (double width, double height, ShapeType shape) BranchDimensions(BaseCrossData data,
                                                                                     bool first,
                                                                                     IReadOnlyDictionary<string, BaseModelData> lookup,
                                                                                     double scale,
                                                                                     ShapeType shape)
    {
        var branch = new BaseModelData
        {
            Style = data.Style,
            StartWidth = first ? data.Branch1Width : data.Branch2Width,
            StartThickness = first ? data.Branch1Thickness : data.Branch2Thickness,
            StartConnectNumber = first ? data.Branch1ConnectNumber : data.Branch2ConnectNumber,
            StartPosition = first ? data.Branch1Position : data.Branch2Position
        };
        return DimensionsAt(branch, true, lookup, scale, shape);
    }

    private static Port MakePort(Vector3 position, Vector3 direction, (double width, double height, ShapeType shape) dimensions)
    {
        return dimensions.shape == ShapeType.Circle
            ? new Port(position, direction, dimensions.width)
            : new Port(position, direction, dimensions.width, dimensions.height, dimensions.shape);
    }

    private static (double width, double height, ShapeType shape) GetConnectedEndpoint(string connection,
                                                                                         Point3d ownerPoint,
                                                                                         IReadOnlyDictionary<string, BaseModelData> lookup)
    {
        if (string.IsNullOrWhiteSpace(connection) || !lookup.TryGetValue(connection, out var connected))
        {
            return (0, 0, ShapeType.Rectangle);
        }

        var endpoint = GetEndpoints(connected)
            .OrderBy(candidate => DistanceSquared(ownerPoint, candidate.point))
            .FirstOrDefault();
        if (endpoint.point == default)
        {
            return (0, 0, ShapeType.Rectangle);
        }

        var width = endpoint.width;
        var height = endpoint.height;
        if (endpoint.shape == ShapeType.Circle)
        {
            var diameter = Positive(width) ? width : height;
            return (diameter, diameter, endpoint.shape);
        }
        return (width, height, endpoint.shape);
    }

    private static ShapeType InferTransitionShape(double width,
                                                   double height,
                                                   string connection,
                                                   Point3d ownerPoint,
                                                   IReadOnlyDictionary<string, BaseModelData> lookup)
    {
        var connected = GetConnectedEndpoint(connection, ownerPoint, lookup);
        if (connected.width > 0 || connected.height > 0) return connected.shape;
        return Positive(width) ? ShapeType.Rectangle : ShapeType.Circle;
    }

    private static IEnumerable<(Point3d point, double width, double height, ShapeType shape)> GetEndpoints(BaseModelData item)
    {
        var shape = ResolveShapeByStyle(item.Style, null);
        yield return (item.StartPosition, item.StartWidth, item.StartThickness, shape);
        yield return (item.EndPosition, item.EndWidth, item.EndThickness, shape);

        if (item is BaseTeeData tee)
        {
            yield return (tee.BranchPosition, tee.BranchWidth, tee.BranchThickness, shape);
        }
        else if (item is BaseCrossData cross)
        {
            yield return (cross.Branch1Position, cross.Branch1Width, cross.Branch1Thickness, shape);
            yield return (cross.Branch2Position, cross.Branch2Width, cross.Branch2Thickness, shape);
        }
    }

    private static Dictionary<string, BaseModelData> BuildLookup(DuctDataForRevit data)
    {
        var lookup = new Dictionary<string, BaseModelData>(StringComparer.OrdinalIgnoreCase);
        void AddRange<T>(IEnumerable<T> items) where T : BaseModelData
        {
            foreach (var item in items ?? Enumerable.Empty<T>())
            {
                if (!string.IsNullOrWhiteSpace(item.Number)) lookup[item.Number] = item;
            }
        }

        AddRange(data.pipes);
        AddRange(data.elbows);
        AddRange(data.tees);
        AddRange(data.crosses);
        AddRange(data.reducers);
        return lookup;
    }

    private static Vector3 ReconstructElbowCorner(BaseElbowData elbow,
                                                   IReadOnlyDictionary<string, BaseModelData> lookup,
                                                   Vector3 start,
                                                   Vector3 end,
                                                   double scale)
    {
        var startAxis = GetConnectionAxis(elbow.StartConnectNumber, elbow.StartPosition, lookup, scale);
        var endAxis = GetConnectionAxis(elbow.EndConnectNumber, elbow.EndPosition, lookup, scale);

        var chord = end - start;
        var chordLength = Math.Sqrt(chord.X * chord.X + chord.Y * chord.Y);
        if (chordLength < PointTolerance)
        {
            throw new InvalidDataException($"Elbow {elbow.Number} has coincident endpoints.");
        }

        var angle = elbow.Angle > 0 ? Math.Clamp(elbow.Angle, 0.001, 179.999) : 90;
        // The JSON angle is the turning angle. Elements measures the angle
        // between the two corner-to-port rays as 180 - turningAngle.
        var offset = chordLength * Math.Tan(angle * Math.PI / 360) / 2;
        var perpendicular = new Vector3(-chord.Y / chordLength, chord.X / chordLength, 0);
        var first = (start + end) / 2 + perpendicular * offset;
        var second = (start + end) / 2 - perpendicular * offset;

        // The angle gives the two possible XY-plane corners. Connection axes
        // select the mirrored solution without changing the supplied angle.
        var firstScore = ElbowCornerScore(first, start, end, startAxis, endAxis);
        var secondScore = ElbowCornerScore(second, start, end, startAxis, endAxis);
        return firstScore >= secondScore ? first : second;
    }

    private static double ElbowCornerScore(Vector3 corner,
                                           Vector3 start,
                                           Vector3 end,
                                           Vector3? startAxis,
                                           Vector3? endAxis)
    {
        var score = 0.0;
        if (startAxis.HasValue)
        {
            score += (start - corner).Unitized().Dot(startAxis.Value);
        }
        if (endAxis.HasValue)
        {
            score += (end - corner).Unitized().Dot(endAxis.Value);
        }
        return score;
    }

    private static Vector3? GetConnectionAxis(string connection,
                                               Point3d anchor,
                                               IReadOnlyDictionary<string, BaseModelData> lookup,
                                               double scale)
    {
        if (string.IsNullOrWhiteSpace(connection) || !lookup.TryGetValue(connection, out var item)) return null;
        var anchorPoint = new Vector3(anchor.X * scale, anchor.Y * scale, anchor.Z * scale);
        var segments = ComponentSegments(item, scale)
            .OrderBy(segment => Math.Min(segment.start.DistanceTo(anchorPoint), segment.end.DistanceTo(anchorPoint)))
            .ToList();
        if (segments.Count == 0) return null;
        var segment = segments[0];
        // Orient the axis away from the connected endpoint.
        var axis = segment.start.DistanceTo(anchorPoint) <= segment.end.DistanceTo(anchorPoint)
            ? segment.end - segment.start
            : segment.start - segment.end;
        return axis.Length() < PointTolerance ? null : axis.Unitized();
    }

    private static IEnumerable<(Vector3 start, Vector3 end)> ComponentSegments(BaseModelData item, double scale)
    {
        var start = new Vector3(item.StartPosition.X * scale, item.StartPosition.Y * scale, item.StartPosition.Z * scale);
        var end = new Vector3(item.EndPosition.X * scale, item.EndPosition.Y * scale, item.EndPosition.Z * scale);
        if (item is BaseElbowData elbow && IsValidCorner(elbow.CornerPosition, elbow.StartPosition, elbow.EndPosition))
        {
            var corner = new Vector3(elbow.CornerPosition.X * scale, elbow.CornerPosition.Y * scale, elbow.CornerPosition.Z * scale);
            yield return (corner, start);
            yield return (corner, end);
            yield break;
        }
        yield return (start, end);
        if (item is BaseTeeData tee)
        {
            var branch = new Vector3(tee.BranchPosition.X * scale, tee.BranchPosition.Y * scale, tee.BranchPosition.Z * scale);
            yield return (ClosestPointOnLine(start, end, branch), branch);
        }
        if (item is BaseCrossData cross)
        {
            var branch1 = new Vector3(cross.Branch1Position.X * scale, cross.Branch1Position.Y * scale, cross.Branch1Position.Z * scale);
            var branch2 = new Vector3(cross.Branch2Position.X * scale, cross.Branch2Position.Y * scale, cross.Branch2Position.Z * scale);
            yield return (ClosestPointOnLine(start, end, branch1), branch1);
            yield return (ClosestPointOnLine(start, end, branch2), branch2);
        }
    }

    private static Vector3 GetModelOffset(DuctDataForRevit data, double scale)
    {
        var points = new List<Point3d>();
        void AddRange<T>(IEnumerable<T> items) where T : BaseModelData
        {
            foreach (var item in items ?? Enumerable.Empty<T>())
            {
                points.Add(item.StartPosition);
                points.Add(item.EndPosition);
                if (item is BaseTeeData tee) points.Add(tee.BranchPosition);
                if (item is BaseCrossData cross)
                {
                    points.Add(cross.Branch1Position);
                    points.Add(cross.Branch2Position);
                }
            }
        }

        AddRange(data.pipes);
        AddRange(data.elbows);
        AddRange(data.tees);
        AddRange(data.crosses);
        AddRange(data.reducers);
        return points.Count == 0
            ? Vector3.Origin
            : new Vector3(points.Average(p => p.X) * scale,
                          points.Average(p => p.Y) * scale,
                          points.Average(p => p.Z) * scale);
    }

    private static bool TryIntersectXY(Vector3 p1, Vector3 d1, Vector3 p2, Vector3 d2, out Vector3 intersection)
    {
        var determinant = d1.X * d2.Y - d1.Y * d2.X;
        if (Math.Abs(determinant) < PointTolerance)
        {
            intersection = default;
            return false;
        }
        var delta = p2 - p1;
        var t = (delta.X * d2.Y - delta.Y * d2.X) / determinant;
        intersection = p1 + d1 * t;
        intersection.Z = (p1.Z + p2.Z) / 2;
        return true;
    }

    private static Vector3 ClosestPointOnLine(Vector3 start, Vector3 end, Vector3 point)
    {
        var line = end - start;
        var lengthSquared = line.LengthSquared();
        if (lengthSquared < PointTolerance) throw new InvalidDataException("Fitting main run has coincident endpoints.");
        return start + line * ((point - start).Dot(line) / lengthSquared);
    }

    private static Vector3 ToVector(Point3d point, double scale, Vector3 offset)
    {
        return new Vector3(point.X * scale, point.Y * scale, point.Z * scale) - offset;
    }

    private static Vector3 Direction(Vector3 from, Vector3 to, string description)
    {
        var direction = to - from;
        if (direction.Length() < PointTolerance) throw new InvalidDataException($"The {description} points cannot be coincident.");
        return direction.Unitized();
    }

    private static bool IsValidCorner(Point3d corner, Point3d start, Point3d end)
    {
        var isOrigin = Math.Abs(corner.X) < PointTolerance && Math.Abs(corner.Y) < PointTolerance && Math.Abs(corner.Z) < PointTolerance;
        return !isOrigin && DistanceSquared(corner, start) > PointTolerance * PointTolerance && DistanceSquared(corner, end) > PointTolerance * PointTolerance;
    }

    private static double DistanceSquared(Point3d a, Point3d b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        var z = a.Z - b.Z;
        return x * x + y * y + z * z;
    }

    private static bool Positive(double value) => value > PointTolerance;

    private static double SanitizeDimension(double value, double fallback)
    {
        if (!Positive(value) || value > MaximumSectionDimension)
        {
            value = Positive(fallback) && fallback <= MaximumSectionDimension
                ? fallback
                : DefaultSectionDimension;
        }
        return Math.Max(value, MinimumDimension);
    }

    private static string NormalizeStyle(string style) => (style ?? string.Empty).Trim();

    private static ShapeType ResolveShape(string style, ShapeType? overrideShape)
    {
        if (overrideShape.HasValue) return overrideShape.Value;
        var normalized = NormalizeStyle(style).ToLowerInvariant();
        if (normalized.Contains("圆") || normalized.Contains("round") || normalized.Contains("circular")) return ShapeType.Circle;
        if (normalized.Contains("椭") || normalized.Contains("oval")) return ShapeType.Oval;
        return ShapeType.Rectangle;
    }

    private static double Diameter((double width, double height, ShapeType shape) dimensions)
    {
        return dimensions.shape == ShapeType.Circle ? dimensions.width : Math.Sqrt(dimensions.width * dimensions.height);
    }

    private static ShapeType ResolveShapeByStyle(string style, ShapeType? overrideShape)
    {
        if (overrideShape.HasValue) return overrideShape.Value;
        var normalized = NormalizeStyle(style).ToLowerInvariant();
        if (normalized.Contains("\u5706") || normalized.Contains("round") || normalized.Contains("circular")) return ShapeType.Circle;
        if (normalized.Contains("\u692d") || normalized.Contains("oval")) return ShapeType.Oval;
        return ShapeType.Rectangle;
    }

    private static DuctDataForRevit CreateSampleData()
    {
        return new DuctDataForRevit
        {
            pipes = { new() { Number = "P-001", Style = "Rectangle", StartPosition = new Point3d(-4, 0, 0), EndPosition = new Point3d(-2, 0, 0), StartWidth = 0.6, EndWidth = 0.6, StartThickness = 0.3, EndThickness = 0.3 } },
            elbows = { new() { Number = "E-001", Style = "Rectangle", StartPosition = new Point3d(-2, 0, 0), EndPosition = new Point3d(0, 2, 0), CornerPosition = new Point3d(0, 0, 0), StartWidth = 0.6, EndWidth = 0.6, StartThickness = 0.3, EndThickness = 0.3, Curvature = 0.5 } },
            tees = { new() { Number = "T-001", Style = "Rectangle", StartPosition = new Point3d(1, 2, 0), EndPosition = new Point3d(3, 2, 0), BranchPosition = new Point3d(2, 4, 0), StartWidth = 0.6, EndWidth = 0.6, BranchWidth = 0.4, StartThickness = 0.3, EndThickness = 0.3, BranchThickness = 0.2 } },
            crosses = { new() { Number = "X-001", Style = "Rectangle", StartPosition = new Point3d(5, 2, 0), EndPosition = new Point3d(7, 2, 0), Branch1Position = new Point3d(6, 4, 0), Branch2Position = new Point3d(6, 0, 0), StartWidth = 0.6, EndWidth = 0.6, Branch1Width = 0.4, Branch2Width = 0.4, StartThickness = 0.3, EndThickness = 0.3, Branch1Thickness = 0.2, Branch2Thickness = 0.2 } },
            reducers = { new() { Number = "R-001", Style = "Rectangle", StartPosition = new Point3d(8, 2, 0), EndPosition = new Point3d(9, 2, 0), StartWidth = 0.6, EndWidth = 0.4, StartThickness = 0.3, EndThickness = 0.2, Length = 1.0 } }
        };
    }
}
