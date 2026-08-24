using System;
using System.Collections.Generic;
using Elements.Flow;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Newtonsoft.Json;

namespace Elements.Fittings
{
    public abstract partial class Fitting
    {
        [Obsolete("Use GetPorts")]
        public Port[] GetConnectors()
        {
            return GetPorts();
        }

        public virtual string GetRepresentationHash()
        {
            return this.GetHashCode().ToString();
        }

        abstract public Port[] GetPorts();

        public abstract Transform GetRotatedTransform();

        public override bool PropagateAdditionalTransform(Transform transform, TransformDirection transformDirection)
        {
            AdditionalTransform.Concatenate(transform);
            return true;
        }

        public override Transform GetPropagatedTransform(TransformDirection transformDirection)
        {
            return AdditionalTransform;
        }

        public override void ClearAdditionalTransform()
        {
            var inverted = new Transform(AdditionalTransform);
            inverted.Invert();
            AdditionalTransform.Concatenate(inverted);
        }

        public override void ApplyAdditionalTransform()
        {
            Transform.Concatenate(AdditionalTransform);
            var connectors = GetPorts();
            foreach (var connector in connectors)
            {
                connector.Position = AdditionalTransform.OfPoint(connector.Position);
            }

            ClearAdditionalTransform();
        }

        public void AssignReferenceBasedOnSection(Section section)
        {
            if (section != null)
            {
                var sectionLocator = new FittingLocator(section);
                ComponentLocator.MatchNetworkSection(sectionLocator);
                if (this is Assembly assembly)
                {
                    assembly.AssignSectionReferenceInternalToAssembly(sectionLocator);
                }
            }
        }

        protected List<SolidOperation> GetExtensions()
        {
            List<SolidOperation> extrudes = new List<SolidOperation>();

            foreach (var port in GetPorts())
            {
                if (port.Dimensions == null || port.Dimensions.Extension.ApproximatelyEquals(0))
                {
                    continue;
                }

                var portWidth = port.ShapeType == ShapeType.Circle ? port.Diameter : (port.Width > 0 ? port.Width : port.Diameter);
                var portHeight = port.ShapeType == ShapeType.Circle ? port.Diameter : (port.Height > 0 ? port.Height : port.Diameter);
                var extensionWidth = port.Dimensions.BodyDiameter;
                if (extensionWidth.ApproximatelyEquals(0) || extensionWidth.ApproximatelyEquals(portWidth)) extensionWidth = portWidth * 1.2;
                var extensionHeight = portHeight * (extensionWidth / Math.Max(portWidth, 0.000001));

                var portTransform = new Transform(port.Position, port.Direction);
                portTransform = portTransform.Concatenated(Transform.Inverted());
                var bigProfile = PipeProfile.Create(port.Diameter, extensionWidth, extensionHeight, port.ShapeType).TransformedPolygon(portTransform);
                var smallProfile = PipeProfile.Create(port.Diameter, portWidth, portHeight, port.ShapeType).TransformedPolygon(portTransform);
                if (bigProfile.Area() < smallProfile.Area())
                {
                    (bigProfile, smallProfile) = (smallProfile, bigProfile);
                }
                Profile profile = new Profile(bigProfile, smallProfile);
                var extrude = new Extrude(profile, port.Dimensions.Extension, portTransform.ZAxis.Unitized());
                extrudes.Add(extrude);
            }

            return extrudes;
        }
    }
}
