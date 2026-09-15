using System;
using Jongreul.XrLocomotion.Radial;
using NUnit.Framework;

namespace Jongreul.XrLocomotion.Tests.Radial
{
    public class RadialSelectorTests
    {
        static (float x, float y) Dir(float degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            return ((float)Math.Sin(radians), (float)Math.Cos(radians));
        }

        static int Point(RadialSelector selector, float degrees)
        {
            (float x, float y) = Dir(degrees);
            return selector.Update(x, y);
        }

        [Test]
        public void AngleOf_UpIsZero_Clockwise()
        {
            Assert.That(RadialSelector.AngleOf(0, 1), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(RadialSelector.AngleOf(1, 0), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(RadialSelector.AngleOf(0, -1), Is.EqualTo(180f).Within(1e-4f));
            Assert.That(RadialSelector.AngleOf(-1, 0), Is.EqualTo(270f).Within(1e-4f));
        }

        [Test]
        public void FourSectors_CardinalDirections()
        {
            Assert.That(new RadialSelector(4).Update(0, 1), Is.EqualTo(0));
            Assert.That(new RadialSelector(4).Update(1, 0), Is.EqualTo(1));
            Assert.That(new RadialSelector(4).Update(0, -1), Is.EqualTo(2));
            Assert.That(new RadialSelector(4).Update(-1, 0), Is.EqualTo(3));
        }

        [Test]
        public void ThreeSectors_BordersAt60_180_300()
        {
            var selector = new RadialSelector(3);

            Assert.That(selector.SectorOf(0), Is.EqualTo(0));
            Assert.That(selector.SectorOf(59), Is.EqualTo(0));
            Assert.That(selector.SectorOf(61), Is.EqualTo(1));
            Assert.That(selector.SectorOf(179), Is.EqualTo(1));
            Assert.That(selector.SectorOf(181), Is.EqualTo(2));
            Assert.That(selector.SectorOf(299), Is.EqualTo(2));
            Assert.That(selector.SectorOf(301), Is.EqualTo(0));
        }

        [Test]
        public void InsideDeadzone_SelectsNothing()
        {
            var selector = new RadialSelector(4, deadzone: 0.35f);

            Assert.That(selector.Update(0.1f, 0.2f), Is.EqualTo(-1));
            Assert.That(selector.IsOpen, Is.False);
        }

        [Test]
        public void NearBorder_StaysOnCurrentSlot_UntilPastHysteresis()
        {
            var selector = new RadialSelector(4, hysteresisDegrees: 8f);
            Assert.That(Point(selector, 0), Is.EqualTo(0));

            Assert.That(Point(selector, 50), Is.EqualTo(0), "경계(45°)를 넘었지만 여유(8°) 안");
            Assert.That(Point(selector, 56), Is.EqualTo(1));
            Assert.That(Point(selector, 40), Is.EqualTo(1), "되돌아와도 여유 안에서는 그대로");
            Assert.That(Point(selector, 35), Is.EqualTo(0));
        }

        [Test]
        public void WrapsAroundTheTop()
        {
            var selector = new RadialSelector(4);

            Assert.That(Point(selector, 350), Is.EqualTo(0));
            Assert.That(Point(selector, 10), Is.EqualTo(0));
            Assert.That(Point(selector, 320), Is.EqualTo(0));
        }

        [Test]
        public void ReleasingTheStick_ConfirmsTheLastSlot_Once()
        {
            var selector = new RadialSelector(4);
            int confirmed = -1;
            int count = 0;
            selector.Confirmed += slot =>
            {
                confirmed = slot;
                count++;
            };

            selector.Update(1, 0);
            selector.Update(0, 0);
            selector.Update(0, 0);

            Assert.That(confirmed, Is.EqualTo(1));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(selector.Current, Is.EqualTo(-1));
            Assert.That(selector.IsOpen, Is.False);
        }

        [Test]
        public void NeverOpened_ConfirmsNothing()
        {
            var selector = new RadialSelector(4);
            int count = 0;
            selector.Confirmed += _ => count++;

            selector.Update(0, 0);

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void InvalidSettings_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RadialSelector(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RadialSelector(4, deadzone: 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RadialSelector(4, hysteresisDegrees: 45f));
        }
    }
}
