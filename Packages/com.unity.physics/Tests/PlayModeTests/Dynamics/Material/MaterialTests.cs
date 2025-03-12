using System;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Physics.Tests.Dynamics.Materials
{
    class MaterialTests
    {
        [Test]
        public void FrictionCombinePolicyTest()
        {
            Material mat1 = new Material();
            Material mat2 = new Material();
            float combinedFriction = 0;

            // GeometricMean Tests
            mat1.FrictionCombinePolicy = Material.CombinePolicy.GeometricMean;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.GeometricMean;

            mat1.Friction = 1.0f;
            mat2.Friction = 0.0f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.0f);

            mat1.Friction = 0.5f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.5f);

            mat1.Friction = 1.0f;
            mat2.Friction = 0.25f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.5f);

            // Minimum Tests
            mat1.FrictionCombinePolicy = Material.CombinePolicy.Minimum;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.Minimum;
            mat1.Friction = 1.0f;
            mat2.Friction = 0.0f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.0f);

            mat1.Friction = 0.5f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.5f);

            mat1.Friction = 1.0f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.5f);

            // Maximum Tests
            mat1.FrictionCombinePolicy = Material.CombinePolicy.Maximum;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.Maximum;
            mat1.Friction = 1.0f;
            mat2.Friction = 0.0f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 1.0f);

            mat1.Friction = 0.5f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.5f);

            mat1.Friction = 2.0f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 2.0f);

            // ArithmeticMean Tests
            mat1.FrictionCombinePolicy = Material.CombinePolicy.ArithmeticMean;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.ArithmeticMean;
            mat1.Friction = 1.0f;
            mat2.Friction = 0.0f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.5f);

            mat1.Friction = 0.25f;
            mat2.Friction = 0.75f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.5f);

            mat1.Friction = 2.0f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 1.25f);

            // Mixed CombinePolicy Tests - Note that max(CombinePolicy of both materials) is used
            mat1.FrictionCombinePolicy = Material.CombinePolicy.GeometricMean;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.ArithmeticMean; // this policy should be used
            mat1.Friction = 2.0f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 1.25f);
            //switch order
            combinedFriction = Material.GetCombinedFriction(mat2, mat1);
            Assert.IsTrue(combinedFriction == 1.25f);

            mat1.FrictionCombinePolicy = Material.CombinePolicy.GeometricMean;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.Maximum; // this policy should be used
            mat1.Friction = 2.0f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 2.0f);
            //switch order
            combinedFriction = Material.GetCombinedFriction(mat2, mat1);
            Assert.IsTrue(combinedFriction == 2.0f);

            mat1.FrictionCombinePolicy = Material.CombinePolicy.GeometricMean;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.Minimum; // this policy should be used
            mat1.Friction = 2.0f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 0.5f);
            //switch order
            combinedFriction = Material.GetCombinedFriction(mat2, mat1);
            Assert.IsTrue(combinedFriction == 0.5f);

            mat1.FrictionCombinePolicy = Material.CombinePolicy.Minimum;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.Maximum; // this policy should be used
            mat1.Friction = 2.0f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 2.0f);
            //switch order
            combinedFriction = Material.GetCombinedFriction(mat2, mat1);
            Assert.IsTrue(combinedFriction == 2.0f);

            mat1.FrictionCombinePolicy = Material.CombinePolicy.Minimum;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.ArithmeticMean; // this policy should be used
            mat1.Friction = 2.0f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 1.25f);
            //switch order
            combinedFriction = Material.GetCombinedFriction(mat2, mat1);
            Assert.IsTrue(combinedFriction == 1.25f);

            mat1.FrictionCombinePolicy = Material.CombinePolicy.Maximum;
            mat2.FrictionCombinePolicy = Material.CombinePolicy.ArithmeticMean; // this policy should be used
            mat1.Friction = 2.0f;
            mat2.Friction = 0.5f;
            combinedFriction = Material.GetCombinedFriction(mat1, mat2);
            Assert.IsTrue(combinedFriction == 1.25f);
            //switch order
            combinedFriction = Material.GetCombinedFriction(mat2, mat1);
            Assert.IsTrue(combinedFriction == 1.25f);
        }

        [Test]
        public void RestitutionCombinePolicyTest()
        {
            Material mat1 = new Material();
            Material mat2 = new Material();
            float combinedRestitution = 0;

            // GeometricMean Tests
            mat1.RestitutionCombinePolicy = Material.CombinePolicy.GeometricMean;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.GeometricMean;

            mat1.Restitution = 1.0f;
            mat2.Restitution = 0.0f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.0f);

            mat1.Restitution = 0.5f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.5f);

            mat1.Restitution = 1.0f;
            mat2.Restitution = 0.25f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.5f);

            // Minimum Tests
            mat1.RestitutionCombinePolicy = Material.CombinePolicy.Minimum;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.Minimum;
            mat1.Restitution = 1.0f;
            mat2.Restitution = 0.0f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.0f);

            mat1.Restitution = 0.5f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.5f);

            mat1.Restitution = 1.0f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.5f);

            // Maximum Tests
            mat1.RestitutionCombinePolicy = Material.CombinePolicy.Maximum;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.Maximum;
            mat1.Restitution = 1.0f;
            mat2.Restitution = 0.0f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 1.0f);

            mat1.Restitution = 0.5f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.5f);

            mat1.Restitution = 2.0f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 2.0f);

            // ArithmeticMean Tests
            mat1.RestitutionCombinePolicy = Material.CombinePolicy.ArithmeticMean;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.ArithmeticMean;
            mat1.Restitution = 1.0f;
            mat2.Restitution = 0.0f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.5f);

            mat1.Restitution = 0.25f;
            mat2.Restitution = 0.75f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.5f);

            mat1.Restitution = 2.0f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 1.25f);

            // Mixed CombinePolicy Tests - Note that max(CombinePolicy of both materials) is used
            mat1.RestitutionCombinePolicy = Material.CombinePolicy.GeometricMean;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.ArithmeticMean; // this policy should be used
            mat1.Restitution = 2.0f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 1.25f);
            //switch order
            combinedRestitution = Material.GetCombinedRestitution(mat2, mat1);
            Assert.IsTrue(combinedRestitution == 1.25f);

            mat1.RestitutionCombinePolicy = Material.CombinePolicy.GeometricMean;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.Maximum; // this policy should be used
            mat1.Restitution = 2.0f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 2.0f);
            //switch order
            combinedRestitution = Material.GetCombinedRestitution(mat2, mat1);
            Assert.IsTrue(combinedRestitution == 2.0f);

            mat1.RestitutionCombinePolicy = Material.CombinePolicy.GeometricMean;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.Minimum; // this policy should be used
            mat1.Restitution = 2.0f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 0.5f);
            //switch order
            combinedRestitution = Material.GetCombinedRestitution(mat2, mat1);
            Assert.IsTrue(combinedRestitution == 0.5f);

            mat1.RestitutionCombinePolicy = Material.CombinePolicy.Minimum;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.Maximum; // this policy should be used
            mat1.Restitution = 2.0f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 2.0f);
            //switch order
            combinedRestitution = Material.GetCombinedRestitution(mat2, mat1);
            Assert.IsTrue(combinedRestitution == 2.0f);

            mat1.RestitutionCombinePolicy = Material.CombinePolicy.Minimum;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.ArithmeticMean; // this policy should be used
            mat1.Restitution = 2.0f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 1.25f);
            //switch order
            combinedRestitution = Material.GetCombinedRestitution(mat2, mat1);
            Assert.IsTrue(combinedRestitution == 1.25f);

            mat1.RestitutionCombinePolicy = Material.CombinePolicy.Maximum;
            mat2.RestitutionCombinePolicy = Material.CombinePolicy.ArithmeticMean; // this policy should be used
            mat1.Restitution = 2.0f;
            mat2.Restitution = 0.5f;
            combinedRestitution = Material.GetCombinedRestitution(mat1, mat2);
            Assert.IsTrue(combinedRestitution == 1.25f);
            //switch order
            combinedRestitution = Material.GetCombinedRestitution(mat2, mat1);
            Assert.IsTrue(combinedRestitution == 1.25f);
        }
    }
}
