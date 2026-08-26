using Elements;
using Elements.Fittings;
using Elements.Flow;
using Elements.Geometry;
using Elements.Geometry.Profiles;
using Elements.MEP.Tests;
using Elements.Serialization.glTF;
using Elements.Validators;
using ExCSS;
using RoutingEngine.Core;
using System;
using Xunit;
using static Elements.MEP.Tests.FittingsTests;

namespace MainTest
{
    public class Program
    {
        static void Main(string[] args)
        {
            //PipeWithTwoReducers(false);
            //MakeElbow();
            //MakeCross();
            //MakeWye();
            MakeReducer();
            //FittingCatalogTreeWithCross();
        }

        public static void TestRouting()
        {
            var profile =
    PipeProfile.Round(
        diameter: 100,
        wallThickness: 5);

            //    var routes =
            //        new[]
            //        {
            //new RoutePolyline
            //{
            //    Id = "R1",

            //    Profile = profile,
            //    //三个直管，两个弯头
            //    Points =
            //    {
            //        new Vector3(0, 0, 0),
            //        new Vector3(0, 1000, 0),
            //        new Vector3(1000, 1000, 0),
            //        new Vector3(1000, 2000, 0)
            //    }
            //}
            //        };


            var routes = new[]
{
    new RoutePolyline
    {
        Id = "MAIN",

        Profile = profile,
        //五个直管，一个四通一个弯头
        Points =
        {
            new Vector3(-1000, 0, 0),
            new Vector3(0, 0, 0),
            new Vector3(1000, 0, 0)
        }
    },

    new RoutePolyline
    {
        Id = "BRANCH",

        Profile = profile,

        Points =
        {
            new Vector3(0, -1000, 0),
            new Vector3(0, 0, 0),
            new Vector3(0, 1000, 0),
            new Vector3(1000, 1000, 0)
        }
    }
};

            var engine =
                new RoutingEngine.Core.RoutingEngine(
                    new RoutingOptions
                    {
                        SnapTolerance = 1.0,
                        IntersectionTolerance = 1.0,
                        AngleToleranceDegrees = 1.0,
                        DefaultBendRadius = 150
                    });

            RoutingResult result =
                engine.Build(routes);
            foreach (var item in result.Components)
            {
                Console.WriteLine(item.Type);
            }
        }

        public static Model MakeModel()
        {
            var model = new Model();

            var length = 10;

            Validator.DisableValidationOnConstruction = true;
            var m = BuiltInMaterials.Concrete;
            var wf = new WideFlangeProfileFactory();
            var p = wf.GetProfileByType(WideFlangeProfileType.W10x100);
            Transform tr = new Transform(Vector3.Origin, 0);
            for (var i = 0; i < 1; i++)
            {
                var start = new Vector3(i, 0, 0);
                var end = new Vector3(i, length, i);

                // The bottom chord
                var bottomChord = new Line(start, end);
                var bottomChordBeam = new Beam(bottomChord, p,tr, m);
                model.AddElement(bottomChordBeam);

                var topChord = new Line(start + new Vector3(0, 0, 5), end + new Vector3(0, 0, 5));
                var topChordBeam = new Beam(topChord, p,tr, m);
                model.AddElement(topChordBeam);

                Vector3 last = default(Vector3);
                for (var j = 0.0; j <= 1.0; j += 0.1)
                {
                    var pt = bottomChord.PointAt(j);
                    var top = pt + new Vector3(0, 0, 5);
                    var panelLine = new Line(pt, top);
                    var panelBeam = new Beam(panelLine, p, tr, m);
                    model.AddElement(panelBeam);

                    if (j > 0.0)
                    {
                        var braceLine = new Line(top, last);
                        var braceBeam = new Beam(braceLine, p,tr, m);
                        model.AddElement(braceBeam);
                    }
                    last = pt;
                }
            }
            return model;
        }

        //public static void MakeCross()
        //{
        //    var cs = new CrossSettings();
        //    var directionTrunk = Vector3.XAxis.Negate();
        //    var position = Vector3.Origin;

        //    var directionA = directionTrunk.Negate();
        //    var directionB = new Transform(position, 90).OfVector(directionA)*100;
        //    var directionC = new Transform(position, -90).OfVector(directionA);

        //    var cross = new Cross(position, directionTrunk, directionA, directionB, directionC, cs);
        //    var model = new Model();
        //    model.AddElement(cross);
        //    string filePath = TestUtils.GetTestPath() + "cross.gltf";
        //    model.ToGlTF(filePath, false);
        //    Console.WriteLine(filePath);
        //}

        public static void MakeElbow()
        {
            ComponentBase.UseRepresentationInstances = true;
            Port.ShowArrows = true;
            var position = new Vector3(1, 0, 1);
            var endDirection = new Vector3(1, 0, 0);
            var otherDirection = new Vector3(0, -1, 1);

            var elbow = new Elements.Fittings.Elbow(position, endDirection, otherDirection, 0.2, 0.2,0.1,ShapeType.Rectangle, FittingTreeRouting.DefaultFittingMaterial);
            var startReferencePipe = new StraightSegment(0, elbow.Start, new Port(elbow.Start.Position + elbow.Start.Direction,
                                                                                   elbow.Start.Direction.Negate(),
                                                                                   elbow.Start.Width,elbow.Start.Height,ShapeType.Rectangle));
            var endReferencePipe = new StraightSegment(0, elbow.End, new Port(elbow.End.Position + elbow.End.Direction,
                                                                               elbow.End.Direction.Negate(),
                                                                               elbow.End.Width, elbow.End.Height, ShapeType.Rectangle));

            position = (2, 2, 2);
            otherDirection = (0, 1, 0);

            var elbow2 = new Elements.Fittings.Elbow(position, endDirection, otherDirection, 0.2, 0.2,0.1,ShapeType.Rectangle, FittingTreeRouting.DefaultFittingMaterial);
            var startReferencePipe2 = new StraightSegment(0, elbow2.Start, new Port(elbow2.Start.Position + elbow2.Start.Direction,
                                                                                     elbow2.Start.Direction.Negate(),
                                                                                     elbow2.Start.Width,elbow2.Start.Height,ShapeType.Rectangle));
            var endReferencePipe2 = new StraightSegment(0, elbow2.End, new Port(elbow2.End.Position + elbow2.End.Direction,
                                                                          elbow2.End.Direction.Negate(),
                                                                          elbow2.End.Width,elbow2.End.Height,ShapeType.Rectangle));

            TestUtils.SaveToGltf(nameof(MakeElbow), new Element[] { elbow, startReferencePipe, endReferencePipe, elbow2, startReferencePipe2, endReferencePipe2 });
        }


        public static void PipeWithTwoReducers(bool isEccentric)
        {
            var tree = new Tree(new[] { "Test" });
            tree.SetOutletPosition(new Vector3());
            var inlet = tree.AddInlet(new Vector3(10, 5, 0));
            tree.SplitConnectionThroughPoint(tree.GetOutgoingConnection(inlet), new Vector3(5, 0, 0));
            var newNode = tree.SplitConnectionThroughPoint(tree.GetOutgoingConnection(inlet), new Vector3(5, 5, 0));
            tree.Connections.ToList().ForEach(c => c.Diameter = 0.1);
            tree.GetOutgoingConnection(newNode).Diameter = 0.05;

            var routing = new SizeAlwaysFromLeafOrTrunk(tree, isEccentric);
            routing.PipeSizeShouldMatchConnection = true;
            routing.PressureCalculator = new HazenWilliamsFullFlow();

            var fittings = routing.BuildFittingTree(out var errors);
            TestUtils.SaveToGltf(nameof(PipeWithTwoReducers), fittings);
            Assert.Empty(errors);
            Assert.Equal(2, fittings.FittingsOfType<Elements.Fittings.Reducer>().Count());
        }


        public static void MakeWye()
        {
            ComponentBase.UseRepresentationInstances = true;
            var branchDirection = new Vector3(0, 1, 1).Unitized();
            var mainDir = new Vector3(0, 1, 0);
            var connectionPoint = new Vector3(1, 0, 1);
            Port.ShowArrows = true;
            var wyeSettings = new WyeSettings
            {
                ShapeType = ShapeType.Oval,
                Width = 0.4,
                Height = 0.2,
                MainWidth = 0.4,
                MainHeight = 0.2,
                BranchWidth = 0.2,
                BranchHeight = 0.1,
                TrunkDistance = 0.2,
                MainDistance = 0.2,
                BranchDistance = 0.3
            };

            var wye = new Wye(connectionPoint,
                              mainDir,
                              branchDirection,
                              wyeSettings,
                              FittingTreeRouting.DefaultFittingMaterial);
            var pipe1 = new StraightSegment(0,
                                        wye.MainBranch,
                                        new Port(wye.MainBranch.Position + mainDir * 2,
                                                 mainDir,
                                                 wye.MainBranch.Width,
                                                 wye.MainBranch.Height,
                                                 wye.MainBranch.ShapeType));
            var pipe2 = new StraightSegment(0,
                                            wye.SideBranch,
                                            new Port(wye.SideBranch.Position + branchDirection,
                                                     branchDirection,
                                                     wye.SideBranch.Width,
                                                     wye.SideBranch.Height,
                                                     wye.SideBranch.ShapeType));
            var pipe3 = new StraightSegment(0,
                                            new Port(wye.Trunk.Position + wye.Trunk.Direction * 2,
                                                     wye.Trunk.Direction,
                                                     wye.Trunk.Width,
                                                     wye.Trunk.Height,
                                                     wye.Trunk.ShapeType),
                                            wye.Trunk
                                            );

            TestUtils.SaveToGltf(nameof(MakeWye), new Element[] { pipe1, pipe2, pipe3, wye });
        }

        
        public static void MakeCross()
        {
            var cs = new CrossSettings();

            var directionTrunk = Vector3.XAxis.Negate();
            var position = Vector3.Origin;

            var directionA = directionTrunk.Negate();
            var directionB = new Transform(position, 90).OfVector(directionA);
            var directionC = new Transform(position, -90).OfVector(directionA);

            var cross = new Elements.Fittings.Cross(position, directionTrunk, directionA, directionB, directionC, cs);
            var model = new Model();
            model.AddElement(cross);
            model.ToGlTF(TestUtils.GetTestPath() + "cross.gltf", false);
        }

        
        public static void MakeReducer()
        {
            Port.ShowArrows = true;
            var reducer = new Elements.Fittings.Reducer(new Vector3(0, 0, 0), new Vector3(0, 1, 0), 0.05, 0.1, 0.08, BuiltInMaterials.Wood);
            var model = new Model();
            model.AddElement(reducer);
            model.ToGlTF(TestUtils.GetTestPath() + "reducer.gltf", false);
        }

        public static void FittingCatalogTreeWithCross()
        {
            Tree tree = GetSampleTreeWithCross(0.01);
            foreach (var connection in tree.Connections)
            {
                connection.Diameter = Units.InchesToMeters(4);
            }
            var crossNode = tree.InternalNodes.FirstOrDefault(n => tree.GetIncomingConnections(n).Count == 3);
            var crossIncomingConnections = tree.GetIncomingConnections(crossNode);
            crossIncomingConnections[0].Diameter = Units.InchesToMeters(2);
            crossIncomingConnections[1].Diameter = Units.InchesToMeters(2);

            tree.GetIncomingConnections(tree.Outlet).First().Diameter = Units.InchesToMeters(4);
            foreach (var inlet in tree.Inlets)
            {
                tree.GetOutgoingConnection(inlet).Diameter = Units.InchesToMeters(4);
            }
            var routing = new FittingTreeRouting(tree);
            routing.FittingCatalog = LoadFittingCatalog();
            routing.PipeSizeShouldMatchConnection = true;
            var fittings = routing.BuildFittingTree(out var errors);
             TestUtils.SaveToGltf(nameof(FittingCatalogTreeWithCross), new Element[] { fittings });
        }


        private static Tree GetSampleTreeWithCross(double flowPerInlet = 5)
        {
            var tree = new Tree(new List<string> { "Tree" });
            var inletPositions = new List<Vector3> {new Vector3(5, 0, 10),
                                                    new Vector3(5, 2, 10) ,
                                                    new Vector3(6, 1, 10) };
            var inlets = new List<Leaf>();
            Node lastNode = null;

            var aboveManifoldInlet = tree.AddInlet(inletPositions[0], flowPerInlet, lastNode);
            var outgoing = tree.GetOutgoingConnection(aboveManifoldInlet);
            lastNode = tree.SplitConnectionThroughPoint(outgoing, new Vector3(5, 1, 9), out var splitConns);
            tree.ConnectVertically(splitConns[0], 0);

            inlets.Add(aboveManifoldInlet);

            foreach (var position in inletPositions.Skip(1))
            {
                var newInlet = tree.AddInlet(position, flowPerInlet, lastNode);
                var newOutgoing = tree.GetOutgoingConnection(newInlet);
                tree.ConnectVertically(newOutgoing, 0);
                inlets.Add(newInlet);
            }

            var outlet = tree.SetOutletPosition(new Vector3(-1, 1, 0));
            var conn = tree.GetIncomingConnections(outlet).First();
            tree.ConnectVertically(conn, 0.5, true);

            foreach (var c in tree.Connections)
            {
                c.Diameter = 0.1;
            }

            tree.Material = ClearPipe;
            return tree;
        }


        private static FittingCatalog LoadFittingCatalog()
        {
            return new FittingCatalog()
            {
                Elbows = ElbowPart.LoadFromCSV("test part catalogs/elbowParts.csv", Units.LengthUnit.Inch),
                Reducers = ReducerPart.LoadFromCSV("test part catalogs/reducerParts.csv", Units.LengthUnit.Inch),
                Tees = TeePart.LoadFromCSV("test part catalogs/teeParts.csv", Units.LengthUnit.Inch),
                Crosses = CrossPart.LoadFromCSV("test part catalogs/crossParts.csv", Units.LengthUnit.Inch)
            };
        }


    }
}
