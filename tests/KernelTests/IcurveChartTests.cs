using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Original chart rebuild tests (spec §5, task T04). Reference values are the
/// delivery package reference_vectors.json RV-CHART group: a unit circle
/// between z=0 and x²+y²=1 with a non-uniform chart, base parameter −2 and
/// base scale 1.7. Expected D1/D2 are properties of the defined circle,
/// independent of how the chart map computes them.
/// </summary>
public class IcurveChartTests
{
    private static readonly double[] Angles = [0.0, 0.17, 0.62, 1.03];
    private static readonly double[] ExpectedParameters =
        [-2.0, -1.7113478784717855, -0.9359810893539275, -0.23160256138323776];
    private static readonly double[] ExpectedScales = [1.7, 1.7376617630025266, 1.7300888028800296];
    private static readonly double[][] ExpectedChordUnits =
    [
        [-0.0848976828024157, 0.9963896745022904, 0.0],
        [-0.3848081888082452, 0.9229965643631171, 0.0],
        [-0.7345477822465785, 0.6785569656238398, 0.0],
    ];

    private static (KernelVector3[] positions, KernelVector3[] tangents) CircleChart()
    {
        var positions = new KernelVector3[Angles.Length];
        var tangents = new KernelVector3[Angles.Length];
        for (var i = 0; i < Angles.Length; i++)
        {
            positions[i] = Vector(Math.Cos(Angles[i]), Math.Sin(Angles[i]), 0);
            // Sense-corrected cross product of support normals: z-axis × radial.
            tangents[i] = Vector(-Math.Sin(Angles[i]), Math.Cos(Angles[i]), 0);
        }
        return (positions, tangents);
    }

    private static AlgorithmStatus BuildCircleMap(out double[] parameters, out double[] scales,
        out KernelVector3[] chordUnits)
    {
        var (positions, tangents) = CircleChart();
        parameters = new double[positions.Length];
        scales = new double[positions.Length - 1];
        chordUnits = new KernelVector3[positions.Length - 1];
        return OriginalChartParameterMap.Build(positions, tangents, -2.0, 1.7,
            parameters, scales, chordUnits, out var segments, out var failure);
    }

    [Fact]
    public void Build_ReproducesReferenceRecursion()
    {
        Assert.Equal(AlgorithmStatus.Success, BuildCircleMap(out var parameters, out var scales, out var chordUnits));
        for (var i = 0; i < 4; i++) Assert.Equal(ExpectedParameters[i], parameters[i], 12);
        for (var i = 0; i < 3; i++) Assert.Equal(ExpectedScales[i], scales[i], 12);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(ExpectedChordUnits[i][0], chordUnits[i].X, 12);
            Assert.Equal(ExpectedChordUnits[i][1], chordUnits[i].Y, 12);
        }
    }

    [Fact]
    public void ChartPoints_AndQueries_MatchReferencePositionsAndJets()
    {
        var (positions, _) = CircleChart();
        Assert.Equal(AlgorithmStatus.Success, BuildCircleMap(out var parameters, out var scales, out var chordUnits));

        (double t, double[] position, double[] d1, double[] d2, int segment)[] queries =
        [
            (-1.8931987150345606, [0.9980208873282734, 0.06288329234769621, 0.0],
                [-0.03699918671456238, 0.587214183238369, 0.0],
                [-0.34502364428074805, -0.029397843696154367, 0.0], 0),
            (-1.424462166498178, [0.9437641861633018, 0.3306193595594795, 0.0],
                [-0.19058773283279, 0.5440391536941673, 0.0],
                [-0.3072312019131942, -0.12808832331372091, 0.0], 1),
            (-0.6753610340047723, [0.716483622292384, 0.6976039126802429, 0.0],
                [-0.403784518905928, 0.41471240265808845, 0.0],
                [-0.22765488685597204, -0.2464397253720785, 0.0], 2),
        ];

        foreach (var (t, position, d1, d2, segment) in queries)
        {
            var x = Vector(position[0], position[1], position[2]);
            // The native-parameter inverse and the chord plane agree with the reference query.
            Assert.Equal(t, OriginalChartParameterMap.InverseParameter(positions, parameters, scales, chordUnits, segment, x), 12);
            Assert.InRange(Math.Abs(OriginalChartParameterMap.PlaneResidual(positions, parameters, scales, chordUnits, segment, t, x)), 0, 1e-12);

            // D1 = T/(f·(e·T)) with the circle tangent (−y, x, 0).
            var tangent = Vector(-x.Y, x.X, 0);
            Assert.Equal(AlgorithmStatus.Success,
                OriginalChartParameterMap.TryParameterDerivative(scales[segment], chordUnits[segment], tangent, out var derivative));
            Assert.Equal(d1[0], derivative.X, 12);
            Assert.Equal(d1[1], derivative.Y, 12);

            // dT/dt along the unit circle: −(dθ/dt)·radial with dθ/dt = |D1|.
            var tangentDerivative = Scale(x, -Math.Sqrt(d1[0] * d1[0] + d1[1] * d1[1]));
            Assert.Equal(AlgorithmStatus.Success,
                OriginalChartParameterMap.TryParameterSecondDerivative(scales[segment], chordUnits[segment], tangent, tangentDerivative, out var second));
            Assert.Equal(d2[0], second.X, 12);
            Assert.Equal(d2[1], second.Y, 12);
        }
    }

    [Fact]
    public void InteriorNode_D1Continuous_D2SideDependent_AndNeverAveraged()
    {
        var (positions, _) = CircleChart();
        Assert.Equal(AlgorithmStatus.Success, BuildCircleMap(out var parameters, out var scales, out var chordUnits));

        var node = parameters[1];
        Assert.True(OriginalChartParameterMap.IsChartNode(parameters, node));
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.LocateSegment(parameters, node, ChartSide.Left, out var left));
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.LocateSegment(parameters, node, ChartSide.Right, out var right));
        Assert.Equal(0, left);
        Assert.Equal(1, right);

        var p = positions[1];
        var tangent = Vector(-p.Y, p.X, 0);
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.TryParameterDerivative(scales[left], chordUnits[left], tangent, out var d1Left));
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.TryParameterDerivative(scales[right], chordUnits[right], tangent, out var d1Right));
        // C¹ recursion: dt/ds = f·(e·T) matches from both sides, so D1 does too.
        Assert.InRange(Math.Abs(d1Left.X - d1Right.X), 0, 1e-12);
        Assert.InRange(Math.Abs(d1Left.Y - d1Right.Y), 0, 1e-12);

        var tangentDerivative = Scale(p, -Math.Sqrt(d1Left.X * d1Left.X + d1Left.Y * d1Left.Y));
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.TryParameterSecondDerivative(scales[left], chordUnits[left], tangent, tangentDerivative, out var d2Left));
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.TryParameterSecondDerivative(scales[right], chordUnits[right], tangent, tangentDerivative, out var d2Right));
        // D2 is side-dependent at the node; the two values differ and must not
        // be averaged into one (§5.5).
        Assert.True(Math.Abs(d2Left.X - d2Right.X) > 1e-9 || Math.Abs(d2Left.Y - d2Right.Y) > 1e-9,
            "node D2 must stay side-dependent");
    }

    [Fact]
    public void Build_DiagnosesBadInputsWithoutHidingThem()
    {
        // Zero-length chord.
        var (positions, tangents) = CircleChart();
        positions[2] = positions[1];
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.InvalidInput, OriginalChartParameterMap.Build(positions, tangents, -2, 1.7,
            parameters, scales, chords, out _, out var failure));
        Assert.Equal(ChartBuildFailure.ZeroLengthChord, failure);

        // Misaligned tangent on a purpose-built chart: T₁·e₀ = 0 is refused,
        // not Abs()-clipped (T₁ ⊥ the first chord direction).
        var misPositions = new KernelVector3[] { Vector(0, 0, 0), Vector(1, 0, 0), Vector(1 + 1e-13, 1, 0) };
        var misTangents = new KernelVector3[] { Vector(1, 0, 0), Vector(0, 1, 0), Vector(0, 1, 0) };
        var misParameters = new double[3];
        var misScales = new double[2];
        var misChords = new KernelVector3[2];
        Assert.Equal(AlgorithmStatus.InvalidInput, OriginalChartParameterMap.Build(misPositions, misTangents, 0, 1.7,
            misParameters, misScales, misChords, out _, out failure));
        Assert.Equal(ChartBuildFailure.MisalignedTangent, failure);

        // Base scale must be finite and positive.
        (positions, tangents) = CircleChart();
        Assert.Equal(AlgorithmStatus.InvalidInput, OriginalChartParameterMap.Build(positions, tangents, -2, 0,
            parameters, scales, chords, out _, out failure));
        Assert.Equal(ChartBuildFailure.BadBaseScale, failure);

        // Global parameter resolution exhausted: t₀ = 9e15 cannot absorb a 0.29 step.
        (positions, tangents) = CircleChart();
        Assert.Equal(AlgorithmStatus.NumericalFailure, OriginalChartParameterMap.Build(positions, tangents, 9e15, 1.7,
            parameters, scales, chords, out _, out failure));
        Assert.Equal(ChartBuildFailure.ParameterResolutionLost, failure);

        // Scale recursion overflow: backward cosine ≈ 1 and forward cosine
        // ≈ 1e-4 amplify 1e305 past double range at the second point.
        var steepPositions = new KernelVector3[] { Vector(0, 0, 0), Vector(1, 0, 0), Vector(0.9999, 1, 0) };
        var steepTangents = new KernelVector3[]
        {
            Vector(1, 0, 0),
            Scale(Vector(1, 2e-4, 0), 1 / Math.Sqrt(1 + 4e-8)),
            Vector(0, 1, 0),
        };
        var steepParameters = new double[3];
        var steepScales = new double[2];
        var steepChords = new KernelVector3[2];
        Assert.Equal(AlgorithmStatus.NumericalFailure, OriginalChartParameterMap.Build(steepPositions, steepTangents, 0, 1e305,
            steepParameters, steepScales, steepChords, out _, out failure));
        Assert.Equal(ChartBuildFailure.ScaleOverflow, failure);
    }

    [Fact]
    public void LocateSegment_RejectsOutOfRange_AndKeepsChartImmutable()
    {
        var (positions, tangents) = CircleChart();
        var originals = (KernelVector3[])positions.Clone();
        Assert.Equal(AlgorithmStatus.Success, BuildCircleMap(out var parameters, out var scales, out var chordUnits));

        Assert.Equal(AlgorithmStatus.InvalidInput, OriginalChartParameterMap.LocateSegment(parameters, parameters[0] - 1e-9, ChartSide.Left, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput, OriginalChartParameterMap.LocateSegment(parameters, parameters[^1] + 1e-9, ChartSide.Right, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput, OriginalChartParameterMap.LocateSegment(parameters, double.NaN, ChartSide.Left, out _));

        for (var i = 0; i < positions.Length; i++)
            Assert.Equal(originals[i].X, positions[i].X, 15);
    }
}
