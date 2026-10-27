using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Regular rolling-ball blend parameter tests (spec §10.1–§10.3 + §10.5 local
/// implicit value, task T12). Fixtures: the RV-TUBE reference (circular spine,
/// constant frame rotation) and the two-plane fillet (translation-invariant
/// frame, quarter arc). D1/D2 are checked against closed forms and central
/// differences inside one smooth piece (§21.2); degenerate contacts are
/// refused, never branch-guessed (GATE-A items stay open).
/// </summary>
public class BlendEvaluationTests
{
    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static double Length(in KernelVector3 v)
        => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    private const double ValueTol = 1e-12;
    private const double DifferenceTol = 1e-6;

    private static BlendFrameInput CircleTubeFrame(double u)
    {
        // Spine circle R=2, radial frame X=(cos u, sin u, 0), Y=(0,0,1), a=1.
        var (sinU, cosU) = Math.SinCos(u);
        return new BlendFrameInput(
            Vector(2 * cosU, 2 * sinU, 0),
            Vector(-2 * sinU, 2 * cosU, 0),
            Vector(-2 * cosU, -2 * sinU, 0),
            Vector(cosU, sinU, 0),
            Vector(-sinU, cosU, 0),
            Vector(-cosU, -sinU, 0),
            Vector(0, 0, 1),
            default,
            default,
            1.0, 0, 0, 0.25);
    }

    [Fact]
    public void CircleTube_Position_MatchesReferenceVectors()
    {
        // RV-TUBE: spine radius 2, s=0.4, section angle 0.43, tube radius 0.25.
        var frame = CircleTubeFrame(0.4);
        Span<KernelVector3> output = new KernelVector3[6];
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.Evaluate(in frame, 0.4, 0.43, 0, output));
        Assert.Equal(2.051425212233302, output[0].X, 12);
        Assert.Equal(0.8673286684807344, output[0].Y, 12);
        Assert.Equal(0.10421770060730269, output[0].Z, 12);
    }

    [Fact]
    public void Evaluate_D2RequiresSixOutputs_AndFailureDoesNotPublish()
    {
        var frame = CircleTubeFrame(0.4);
        Span<KernelVector3> tooSmall = stackalloc KernelVector3[5];
        Assert.Equal(AlgorithmStatus.OutputTooSmall,
            BlendEvaluation.Evaluate(in frame, 0.4, 0.2, 2, tooSmall));

        Span<KernelVector3> exact = stackalloc KernelVector3[6];
        Assert.Equal(AlgorithmStatus.Success,
            BlendEvaluation.Evaluate(in frame, 0.4, 0.2, 2, exact));

        var failing = new BlendFrameInput(frame.Spine, frame.SpineD1,
            Vector(double.NaN, 0, 0), frame.X, frame.XD1, frame.XD2,
            frame.Y, frame.YD1, frame.YD2, frame.Arc, frame.ArcD1, frame.ArcD2, frame.Radius);
        exact.Fill(Vector(42, 42, 42));
        Assert.Equal(AlgorithmStatus.NumericalFailure,
            BlendEvaluation.Evaluate(in failing, 0.4, 0.2, 2, exact));
        Assert.Equal(42, exact[0].X);
        Assert.Equal(42, exact[5].Z);
    }

    [Fact]
    public void ContactFromWitness_ReturnsPointOnSupport_NotOffsetCenter()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryContactFromSpineWitness(
            in plane, 1, 2, 1, 1, out var contact, out var normal));
        Assert.Equal(1, contact.X, 12);
        Assert.Equal(2, contact.Y, 12);
        Assert.Equal(0, contact.Z, 12);
        Assert.Equal(1, normal.Z, 12);

        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryContactFromSpineWitness(
            in plane, 1, 2, 1, ParasolidConstants.PK_TOPOL_sense_negative_c,
            out var negativeContact, out var negativeNormal));
        Assert.Equal(contact.X, negativeContact.X, 12);
        Assert.Equal(contact.Y, negativeContact.Y, 12);
        Assert.Equal(contact.Z, negativeContact.Z, 12);
        Assert.Equal(-1, negativeNormal.Z, 12);
    }

    [Fact]
    public void CircleTube_Derivatives_MatchClosedFormsAndDifferences()
    {
        var frame = CircleTubeFrame(0.4);
        const double u = 0.4, v = 0.43;
        var (sinT, cosT) = Math.SinCos(v);
        var (sinU, cosU) = Math.SinCos(u);

        Span<KernelVector3> output = new KernelVector3[6];
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.Evaluate(in frame, u, v, 2, output));

        // Closed forms for this fixture: B_u = X'(2 + r·cosθ) (C' = 2X'),
        // B_v = r·V, B_uu = −X(2 + r·cosθ), B_uv = −r·sinθ·X', B_vv = −r·E.
        var xPrime = Vector(-sinU, cosU, 0);
        var x = Vector(cosU, sinU, 0);
        var e = Add(Scale(x, cosT), Vector(0, 0, sinT));
        var vVec = Sub(Vector(0, 0, cosT), Scale(x, sinT));
        Assert.Equal(Scale(xPrime, 2 + 0.25 * cosT).X, output[1].X, 12);
        Assert.Equal(Scale(xPrime, 2 + 0.25 * cosT).Y, output[1].Y, 12);
        Assert.Equal(Scale(vVec, 0.25).X, output[2].X, 12);
        Assert.Equal(Scale(vVec, 0.25).Z, output[2].Z, 12);
        var buu = Scale(x, -(2 + 0.25 * cosT));
        Assert.InRange(Math.Abs(output[3].X - buu.X), 0, ValueTol);
        Assert.InRange(Math.Abs(output[3].Z - buu.Z), 0, ValueTol);
        var buv = Scale(xPrime, -0.25 * sinT);
        Assert.InRange(Math.Abs(output[4].X - buv.X), 0, ValueTol);
        Assert.InRange(Math.Abs(output[4].Y - buv.Y), 0, ValueTol);
        Assert.InRange(Math.Abs(output[5].X - (-0.25 * e.X)), 0, ValueTol);
        Assert.InRange(Math.Abs(output[5].Z - (-0.25 * e.Z)), 0, ValueTol);

        // Central differences inside the smooth piece (§21.2). The frame is a
        // per-u snapshot, so u-differences rebuild the frame at u±h.
        const double h = 1e-5;
        Span<KernelVector3> plus = new KernelVector3[6];
        Span<KernelVector3> minus = new KernelVector3[6];
        var framePlus = CircleTubeFrame(u + h);
        var frameMinus = CircleTubeFrame(u - h);
        BlendEvaluation.Evaluate(in framePlus, u + h, v, 0, plus);
        BlendEvaluation.Evaluate(in frameMinus, u - h, v, 0, minus);
        Assert.InRange(Length(Sub(output[1], Scale(Sub(plus[0], minus[0]), 1 / (2 * h)))), 0, DifferenceTol);
        BlendEvaluation.Evaluate(in frame, u, v + h, 0, plus);
        BlendEvaluation.Evaluate(in frame, u, v - h, 0, minus);
        Assert.InRange(Length(Sub(output[2], Scale(Sub(plus[0], minus[0]), 1 / (2 * h)))), 0, DifferenceTol);
        // Second derivatives from differencing the analytic first derivatives.
        BlendEvaluation.Evaluate(in framePlus, u + h, v, 1, plus);
        BlendEvaluation.Evaluate(in frameMinus, u - h, v, 1, minus);
        Assert.InRange(Length(Sub(output[3], Scale(Sub(plus[1], minus[1]), 1 / (2 * h)))), 0, DifferenceTol);
        Assert.InRange(Length(Sub(output[4], Scale(Sub(plus[2], minus[2]), 1 / (2 * h)))), 0, DifferenceTol);
        BlendEvaluation.Evaluate(in frame, u, v + h, 1, plus);
        BlendEvaluation.Evaluate(in frame, u, v - h, 1, minus);
        Assert.InRange(Length(Sub(output[5], Scale(Sub(plus[2], minus[2]), 1 / (2 * h)))), 0, DifferenceTol);
    }

    [Fact]
    public void CircleTube_LocalImplicit_MatchesReferenceVectors()
    {
        // RV-TUBE D, implicit_gradient at the reference point (§10.5 value and
        // gradient; the Hessian arrives with the T13 elimination plan).
        var frame = CircleTubeFrame(0.4);
        var point = Vector(2.051425212233302, 0.8673286684807344, 0.10421770060730269);
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryLocalImplicit(
            in frame.Spine, in frame.SpineD1, in frame.SpineD2, 0.25, in point,
            out var value, out var gradient, out var eliminationD));
        Assert.InRange(Math.Abs(value), 0, 1e-12);
        Assert.Equal(0.4186064484550638, gradient.X, 12);
        Assert.Equal(0.17698396772686675, gradient.Y, 12);
        Assert.Equal(0.20843540121460538, gradient.Z, 12);
        Assert.Equal(4.454482874837443, eliminationD, 12);
    }

    [Fact]
    public void TwoPlaneFillet_FrameAndEvaluation_MatchCylinderClosedForm()
    {
        // Fillet of planes z=0 and x=0 with r=1: spine (1,u,1), contacts
        // (1,u,0) and (0,u,1). Quarter arc around the translation-invariant frame.
        var spine = Vector(1, 0, 1);
        var tangent = Vector(0, 1, 0);
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, Vector(1, 0, 0), Vector(0, 0, 1), 1.0,
            out var x, out var y, out var arc));
        Assert.Equal(0, x.X, 12); Assert.Equal(0, x.Y, 12); Assert.Equal(-1, x.Z, 12);
        Assert.Equal(-1, y.X, 12); Assert.Equal(0, y.Y, 12); Assert.Equal(0, y.Z, 12);
        Assert.Equal(Math.PI / 2, arc, 12);

        // The frame input is the per-u snapshot; evaluate at u=0.7.
        var frame = new BlendFrameInput(
            Vector(1, 0.7, 1), Vector(0, 1, 0), default,
            x, default, default,
            y, default, default,
            arc, 0, 0, 1.0);

        for (var i = 0; i <= 4; i++)
        {
            var v = i / 4.0;
            var theta = v * Math.PI / 2;
            Span<KernelVector3> output = new KernelVector3[6];
            Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.Evaluate(in frame, 0.7, v, 0, output));
            // Closed form: the fillet is the cylinder (x−1)² + (z−1)² = 1.
            Assert.Equal(1 - Math.Sin(theta), output[0].X, 12);
            Assert.Equal(0.7, output[0].Y, 12);
            Assert.Equal(1 - Math.Cos(theta), output[0].Z, 12);
            Assert.InRange(Math.Abs(output[0].X * output[0].X - 2 * output[0].X
                + output[0].Z * output[0].Z - 2 * output[0].Z + 1), 0, 1e-12);
        }

        // D1: B_u = (0,1,0); B_v = a(−cosθ, 0, sinθ). D2: B_vv = −a²E, others 0.
        Span<KernelVector3> jets = new KernelVector3[6];
        BlendEvaluation.Evaluate(in frame, 0.7, 0.3, 2, jets);
        Assert.Equal(0, jets[1].X, 12); Assert.Equal(1, jets[1].Y, 12); Assert.Equal(0, jets[1].Z, 12);
        var jetTheta = 0.3 * Math.PI / 2;
        Assert.InRange(Math.Abs(jets[2].X - (-Math.PI / 2 * Math.Cos(jetTheta))), 0, ValueTol);
        Assert.InRange(Math.Abs(jets[2].Z - (Math.PI / 2 * Math.Sin(jetTheta))), 0, ValueTol);
        Assert.InRange(Length(jets[3]), 0, ValueTol);
        Assert.InRange(Length(jets[4]), 0, ValueTol);
        // B_vv = −a²E with E = (−sinθ, 0, −cosθ) for this frame.
        Assert.InRange(Math.Abs(jets[5].X - (Math.PI * Math.PI / 4 * Math.Sin(jetTheta))), 0, ValueTol);
        Assert.InRange(Math.Abs(jets[5].Z - (Math.PI * Math.PI / 4 * Math.Cos(jetTheta))), 0, ValueTol);
    }

    [Fact]
    public void SwappedContacts_TraverseTheSameArcInReverse()
    {
        // Sense resolution swaps the contact roles: the same physical arc is
        // parametrized from the other end (§10.2 spine-sense mapping).
        var spine = Vector(1, 0, 1);
        var tangent = Vector(0, 1, 0);
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, Vector(1, 0, 0), Vector(0, 0, 1), 1.0, out _, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, Vector(0, 0, 1), Vector(1, 0, 0), 1.0,
            out var xSwapped, out var ySwapped, out var arcSwapped));
        Assert.Equal(-Math.PI / 2, arcSwapped, 12);

        var frameA = new BlendFrameInput(Vector(1, 0, 1), Vector(0, 1, 0), default,
            Vector(0, 0, -1), default, default, Vector(-1, 0, 0), default, default,
            Math.PI / 2, 0, 0, 1.0);
        var frameB = new BlendFrameInput(Vector(1, 0, 1), Vector(0, 1, 0), default,
            xSwapped, default, default, ySwapped, default, default,
            arcSwapped, 0, 0, 1.0);
        Span<KernelVector3> a = new KernelVector3[1];
        Span<KernelVector3> b = new KernelVector3[1];
        BlendEvaluation.Evaluate(in frameA, 0, 0.25, 0, a);
        BlendEvaluation.Evaluate(in frameB, 0, 0.75, 0, b);
        Assert.InRange(Math.Abs(a[0].X - b[0].X), 0, ValueTol);
        Assert.InRange(Math.Abs(a[0].Z - b[0].Z), 0, ValueTol);
    }

    [Fact]
    public void DegenerateContacts_AreRefused_NotBranchGuessed()
    {
        var spine = Vector(1, 0, 1);
        var tangent = Vector(0, 1, 0);
        // Near-coincident contacts (a ≈ 0).
        Assert.Equal(AlgorithmStatus.Singular, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, Vector(1, 0, 0), Vector(1, 1e-12, 0), 1.0, out _, out _, out _));
        // Near-antipodal contacts (a ≈ π): ambiguous arc direction.
        Assert.Equal(AlgorithmStatus.Singular, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, Vector(1, 0, 0), Vector(1, 0, 2), 1.0, out _, out _, out _));
        // Zero spine tangent.
        Assert.Equal(AlgorithmStatus.Singular, BlendEvaluation.TryBuildFrame(
            in spine, default, Vector(1, 0, 0), Vector(0, 0, 1), 1.0, out _, out _, out _));
        // Contacts not at radius distance.
        Assert.Equal(AlgorithmStatus.InvalidInput, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, Vector(1, 0, 0.5), Vector(0, 0, 1), 1.0, out _, out _, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, Vector(1, 0, 0), Vector(0, 0, 1), 0, out _, out _, out _));
    }

    [Fact]
    public void RegularConstruction_RejectsInconsistentRangesWithoutAveraging()
    {
        Assert.True(BlendEvaluation.IsRegularConstruction(0.25, 0.25, 0.25));
        // Signed ranges (sense-negated) still match by magnitude.
        Assert.True(BlendEvaluation.IsRegularConstruction(0.25, -0.25, 0.25));
        // Inconsistent magnitudes: refused, never averaged (§10.1).
        Assert.False(BlendEvaluation.IsRegularConstruction(0.25, 0.2, 0.25));
        Assert.False(BlendEvaluation.IsRegularConstruction(0.25, 0.25, 0.3));
        // Zero radius and the range=[0,0] construction never enter the tube path.
        Assert.False(BlendEvaluation.IsRegularConstruction(0.25, 0, 0));
        Assert.False(BlendEvaluation.IsRegularConstruction(0, 0, 0));
        Assert.False(BlendEvaluation.IsRegularConstruction(double.NaN, 0.25, 0.25));
    }

    [Fact]
    public void TryBuildFrame_WithPreviousArcWitness_UnwrapsAcrossPiBranchCut()
    {
        var spine = Vector(0, 0, 0);
        var tangent = Vector(0, 0, 1);
        var contactStart = Vector(1, 0, 0);
        var contactEndNearMinusPi = Vector(Math.Cos(-Math.PI + 0.02), Math.Sin(-Math.PI + 0.02), 0);

        // Without witness, principal atan2 returns ≈ -π + 0.02.
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, in contactStart, in contactEndNearMinusPi, 1.0,
            out _, out _, out var principalArc));
        Assert.InRange(principalArc, -Math.PI, -Math.PI + 0.05);

        // With witness near +π - 0.02, unwraps to ≈ +π + 0.02 (+2π branch).
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, in contactStart, in contactEndNearMinusPi, 1.0,
            out _, out _, out var unwrappedArc, previousArc: Math.PI - 0.02));
        Assert.Equal(principalArc + 2 * Math.PI, unwrappedArc, 12);

        // Exact antipodal contact is accepted when a witness disambiguates the ±π branch.
        var antipodalEnd = Vector(-1, 0, 0);
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, in contactStart, in antipodalEnd, 1.0,
            out _, out _, out var antipodalPlusPi, previousArc: Math.PI - 0.01));
        Assert.Equal(Math.PI, antipodalPlusPi, 12);
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryBuildFrame(
            in spine, in tangent, in contactStart, in antipodalEnd, 1.0,
            out _, out _, out var antipodalMinusPi, previousArc: -Math.PI + 0.01));
        Assert.Equal(-Math.PI, antipodalMinusPi, 12);
    }

    [Fact]
    public void TryBuildFrameJet_PropagatesAnalyticalDerivativesForTwistedVariableArcSection()
    {
        // Helical spine C(u) = (2 cos u, 2 sin u, 0.5 u), radius r = 0.25.
        // Construct start and end contacts in the normal plane of C'(u) with
        // u-varying roll angle φ(u) = 0.3 u and u-varying arc angle a(u) = 1.1 + 0.2 sin(u)
        // so X', X'', Y', Y'', a', a'' are all non-zero.
        const double radius = 0.25;
        static void EvalSection(
            double u,
            out KernelVector3 c, out KernelVector3 c1, out KernelVector3 c2, out KernelVector3 c3,
            out KernelVector3 qStart, out KernelVector3 qEnd)
        {
            var (s, co) = Math.SinCos(u);
            c = Vector(2 * co, 2 * s, 0.5 * u);
            c1 = Vector(-2 * s, 2 * co, 0.5);
            c2 = Vector(-2 * co, -2 * s, 0);
            c3 = Vector(2 * s, -2 * co, 0);

            // Orthonormal basis (N, B) of the normal plane orthogonal to C'(u):
            // N(u) = (cos u, sin u, 0) is already orthogonal to C'(u)!
            // B(u) = unit(C'(u) × N(u)) = (-0.5 sin u, 0.5 cos u, -2) / sqrt(4.25).
            var n = Vector(co, s, 0);
            var bScale = 1.0 / Math.Sqrt(4.25);
            var b = Vector(-0.5 * s * bScale, 0.5 * co * bScale, -2.0 * bScale);

            var phi = 0.3 * u;
            var arc = 1.1 + 0.2 * Math.Sin(u);
            var (sinPhi, cosPhi) = Math.SinCos(phi);
            var (sinEnd, cosEnd) = Math.SinCos(phi + arc);

            var xDir = Add(Scale(n, cosPhi), Scale(b, sinPhi));
            // Note: Y = unit(C' × X). Since C' × N = sqrt(4.25) B and C' × B = -sqrt(4.25) N,
            // unit(C' × (N cos φ + B sin φ)) = B cos φ - N sin φ, which is the derivative of xDir w.r.t. φ!
            // Therefore X cos a + Y sin a = N cos(φ + a) + B sin(φ + a).
            var endDir = Add(Scale(n, cosEnd), Scale(b, sinEnd));

            qStart = Add(c, Scale(xDir, radius));
            qEnd = Add(c, Scale(endDir, radius));
        }

        const double u0 = 0.6;
        const double h = 1e-6;
        EvalSection(u0, out var c, out var c1, out var c2, out var c3, out var qStart0, out var qEnd0);
        EvalSection(u0 + h, out _, out _, out _, out _, out var qStartP, out var qEndP);
        EvalSection(u0 - h, out _, out _, out _, out _, out var qStartM, out var qEndM);
        EvalSection(u0 + 2 * h, out _, out _, out _, out _, out var qStartPP, out var qEndPP);
        EvalSection(u0 - 2 * h, out _, out _, out _, out _, out var qStartMM, out var qEndMM);

        // 4th-order central differences for the input contact curves Q_start(u), Q_end(u)
        var qStartD1 = Scale(Sub(Scale(Sub(qStartP, qStartM), 8), Sub(qStartPP, qStartMM)), 1.0 / (12 * h));
        var qEndD1 = Scale(Sub(Scale(Sub(qEndP, qEndM), 8), Sub(qEndPP, qEndMM)), 1.0 / (12 * h));
        var qStartD2 = Scale(
            Sub(Add(Scale(Add(qStartP, qStartM), 16), Scale(qStart0, -30)), Add(qStartPP, qStartMM)),
            1.0 / (12 * h * h));
        var qEndD2 = Scale(
            Sub(Add(Scale(Add(qEndP, qEndM), 16), Scale(qEnd0, -30)), Add(qEndPP, qEndMM)),
            1.0 / (12 * h * h));

        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryBuildFrameJet(
            in c, in c1, in c2, in c3,
            in qStart0, in qStartD1, in qStartD2,
            in qEnd0, in qEndD1, in qEndD2,
            radius, out var frame));

        // Verify analytical arc(u), arc'(u), arc''(u) against closed form a(u) = 1.1 + 0.2 sin(u).
        Assert.Equal(1.1 + 0.2 * Math.Sin(u0), frame.Arc, 10);
        Assert.InRange(Math.Abs(frame.ArcD1 - 0.2 * Math.Cos(u0)), 0, 1e-8);
        Assert.InRange(Math.Abs(frame.ArcD2 - (-0.2 * Math.Sin(u0))), 0, 1e-6);

        // Verify Y, YD1, YD2 against central differences of TryBuildFrame at u0 ± h.
        EvalSection(u0 + h, out var cP, out var c1P, out _, out _, out _, out _);
        EvalSection(u0 - h, out var cM, out var c1M, out _, out _, out _, out _);
        BlendEvaluation.TryBuildFrame(in cP, in c1P, in qStartP, in qEndP, radius, out var xP, out var yP, out _);
        BlendEvaluation.TryBuildFrame(in cM, in c1M, in qStartM, in qEndM, radius, out var xM, out var yM, out _);

        var yD1Fd = Scale(Sub(yP, yM), 1.0 / (2 * h));
        var yD2Fd = Scale(Add(Sub(yP, Scale(frame.Y, 2)), yM), 1.0 / (h * h));
        Assert.InRange(Length(Sub(frame.YD1, yD1Fd)), 0, 1e-7);
        Assert.InRange(Length(Sub(frame.YD2, yD2Fd)), 0, 1e-5);
    }
}
