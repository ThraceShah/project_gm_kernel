import { IndexData, ManifestData, MeshData } from '../types/contract';

/**
 * DEV FIXTURE DATA
 * Strictly marked for development and interactive testing.
 * NOT bundled as production data fallback.
 */
export const IS_DEV_FIXTURE = true;

// Helper to make a box face
function makeQuad(
  p0: [number, number, number],
  p1: [number, number, number],
  p2: [number, number, number],
  p3: [number, number, number]
): { positions: number[]; indices: number[] } {
  return {
    positions: [...p0, ...p1, ...p2, ...p3],
    indices: [0, 1, 2, 0, 2, 3],
  };
}

// 1. Box Before Fillet
export const MESH_BOX_BEFORE: MeshData = {
  schemaVersion: 1,
  bodyId: 'body_box_before_01',
  source: {
    kernel: 'GM Kernel',
    geometryAuthor: 'SolidModeling::CreateBlock',
    tessellator: 'PK_TOPOL_render_facet',
    commit: 'a1e4500',
    chordTolerance: 0.0005,
  },
  faces: [
    // Bottom Z=0
    { id: 'face_z_min', ...makeQuad([0, 0, 0], [10, 0, 0], [10, 10, 0], [0, 10, 0]), role: 'unchanged' },
    // Top Z=10 (Support face adjacent to blend edge)
    { id: 'face_z_max', ...makeQuad([0, 0, 10], [0, 10, 10], [10, 10, 10], [10, 0, 10]), role: 'support' },
    // Front Y=0
    { id: 'face_y_min', ...makeQuad([0, 0, 0], [0, 0, 10], [10, 0, 10], [10, 0, 0]), role: 'unchanged' },
    // Back Y=10 (Support face adjacent to blend edge)
    { id: 'face_y_max', ...makeQuad([0, 10, 0], [10, 10, 0], [10, 10, 10], [0, 10, 10]), role: 'support' },
    // Left X=0
    { id: 'face_x_min', ...makeQuad([0, 0, 0], [0, 10, 0], [0, 10, 10], [0, 0, 10]), role: 'unchanged' },
    // Right X=10
    { id: 'face_x_max', ...makeQuad([10, 0, 0], [10, 0, 10], [10, 10, 10], [10, 10, 0]), role: 'unchanged' },
  ],
  edges: [
    // The target edge to blend: Top-Back edge along X (Y=10, Z=10)
    { id: 'edge_blend_target', positions: [0, 10, 10, 10, 10, 10], role: 'selected' },
    // Other wireframe edges
    { id: 'edge_01', positions: [0, 0, 0, 10, 0, 0], role: 'unchanged' },
    { id: 'edge_02', positions: [10, 0, 0, 10, 10, 0], role: 'unchanged' },
    { id: 'edge_03', positions: [10, 10, 0, 0, 10, 0], role: 'unchanged' },
    { id: 'edge_04', positions: [0, 10, 0, 0, 0, 0], role: 'unchanged' },
    { id: 'edge_05', positions: [0, 0, 10, 10, 0, 10], role: 'unchanged' },
    { id: 'edge_06', positions: [10, 0, 10, 10, 10, 10], role: 'support' },
    { id: 'edge_07', positions: [0, 10, 10, 0, 0, 10], role: 'support' },
    { id: 'edge_08', positions: [0, 0, 0, 0, 0, 10], role: 'unchanged' },
    { id: 'edge_09', positions: [10, 0, 0, 10, 0, 10], role: 'unchanged' },
    { id: 'edge_10', positions: [10, 10, 0, 10, 10, 10], role: 'unchanged' },
    { id: 'edge_11', positions: [0, 10, 0, 0, 10, 10], role: 'unchanged' },
  ],
};

// 2. Box After Fillet (Radius R=2 along Y=10, Z=10)
export const MESH_BOX_AFTER_GM: MeshData = {
  schemaVersion: 1,
  bodyId: 'body_box_after_gm_01',
  source: {
    kernel: 'GM Kernel',
    geometryAuthor: 'BlendRollingBall::Execute',
    tessellator: 'PK_TOPOL_render_facet',
    commit: 'a1e4500',
    chordTolerance: 0.0005,
  },
  faces: [
    // Bottom Z=0 (unchanged)
    { id: 'face_z_min', ...makeQuad([0, 0, 0], [10, 0, 0], [10, 10, 0], [0, 10, 0]), role: 'unchanged' },
    // Top Z=10 (modified: stops at Y=8 instead of 10)
    { id: 'face_z_max_mod', ...makeQuad([0, 0, 10], [0, 8, 10], [10, 8, 10], [10, 0, 10]), role: 'modified' },
    // Front Y=0 (unchanged)
    { id: 'face_y_min', ...makeQuad([0, 0, 0], [0, 0, 10], [10, 0, 10], [10, 0, 0]), role: 'unchanged' },
    // Back Y=10 (modified: stops at Z=8 instead of 10)
    { id: 'face_y_max_mod', ...makeQuad([0, 10, 0], [10, 10, 0], [10, 10, 8], [0, 10, 8]), role: 'modified' },
    // Left X=0 (modified polygon)
    {
      id: 'face_x_min_mod',
      positions: [0, 0, 0, 0, 10, 0, 0, 10, 8, 0, 8, 10, 0, 0, 10],
      indices: [0, 1, 2, 0, 2, 3, 0, 3, 4],
      role: 'modified',
    },
    // Right X=10 (modified polygon)
    {
      id: 'face_x_max_mod',
      positions: [10, 0, 0, 10, 0, 10, 10, 8, 10, 10, 10, 8, 10, 10, 0],
      indices: [0, 1, 2, 0, 2, 3, 0, 3, 4],
      role: 'modified',
    },
    // New Fillet cylindrical arc face (between Y=8..10, Z=8..10, R=2)
    {
      id: 'face_fillet_surface',
      positions: [
        0, 8, 10,  10, 8, 10,  10, 8.58, 9.85, 0, 8.58, 9.85,
        0, 8.58, 9.85, 10, 8.58, 9.85, 10, 9.41, 9.41, 0, 9.41, 9.41,
        0, 9.41, 9.41, 10, 9.41, 9.41, 10, 9.85, 8.58, 0, 9.85, 8.58,
        0, 9.85, 8.58, 10, 9.85, 8.58, 10, 10, 8,     0, 10, 8,
      ],
      indices: [
        0, 1, 2, 0, 2, 3,
        4, 5, 6, 4, 6, 7,
        8, 9, 10, 8, 10, 11,
        12, 13, 14, 12, 14, 15,
      ],
      role: 'newBlend',
    },
  ],
  edges: [
    { id: 'edge_fillet_spring_top', positions: [0, 8, 10, 10, 8, 10], role: 'newBlend' },
    { id: 'edge_fillet_spring_back', positions: [0, 10, 8, 10, 10, 8], role: 'newBlend' },
    { id: 'edge_base_01', positions: [0, 0, 0, 10, 0, 0], role: 'unchanged' },
    { id: 'edge_base_02', positions: [10, 0, 0, 10, 10, 0], role: 'unchanged' },
    { id: 'edge_base_03', positions: [10, 10, 0, 0, 10, 0], role: 'unchanged' },
    { id: 'edge_base_04', positions: [0, 10, 0, 0, 0, 0], role: 'unchanged' },
    { id: 'edge_vert_01', positions: [0, 0, 0, 0, 0, 10], role: 'unchanged' },
    { id: 'edge_vert_02', positions: [10, 0, 0, 10, 0, 10], role: 'unchanged' },
    { id: 'edge_vert_03', positions: [0, 10, 0, 0, 10, 8], role: 'unchanged' },
    { id: 'edge_vert_04', positions: [10, 10, 0, 10, 10, 8], role: 'unchanged' },
  ],
};

// 3. Box After Fillet (Parasolid PK ground truth)
export const MESH_BOX_AFTER_PK: MeshData = {
  ...MESH_BOX_AFTER_GM,
  bodyId: 'body_box_after_pk_01',
  source: {
    kernel: 'Parasolid v38.0',
    geometryAuthor: 'PK_BODY_fix_blends',
    tessellator: 'PK_TOPOL_render_facet',
    commit: 'Parasolid-v38.0.165',
    chordTolerance: 0.0005,
  },
};

// 4. Boolean Subtract Plate Before
export const MESH_BOOLEAN_BEFORE: MeshData = {
  schemaVersion: 1,
  bodyId: 'body_bool_before',
  source: {
    kernel: 'GM Kernel',
    geometryAuthor: 'SolidModeling::CreateBlock',
    tessellator: 'PK_TOPOL_render_facet',
    commit: 'a1e4500',
    chordTolerance: 0.001,
  },
  faces: [
    { id: 'plate_bottom', ...makeQuad([-5, -5, 0], [5, -5, 0], [5, 5, 0], [-5, 5, 0]), role: 'unchanged' },
    { id: 'plate_top', ...makeQuad([-5, -5, 4], [-5, 5, 4], [5, 5, 4], [5, -5, 4]), role: 'selected' },
    { id: 'plate_front', ...makeQuad([-5, -5, 0], [-5, -5, 4], [5, -5, 4], [5, -5, 0]), role: 'unchanged' },
    { id: 'plate_back', ...makeQuad([-5, 5, 0], [5, 5, 0], [5, 5, 4], [-5, 5, 4]), role: 'unchanged' },
    { id: 'plate_left', ...makeQuad([-5, -5, 0], [-5, 5, 0], [-5, 5, 4], [-5, -5, 4]), role: 'unchanged' },
    { id: 'plate_right', ...makeQuad([5, -5, 0], [5, -5, 4], [5, 5, 4], [5, 5, 0]), role: 'unchanged' },
  ],
  edges: [
    { id: 'plate_edge_t1', positions: [-5, -5, 4, 5, -5, 4], role: 'unchanged' },
    { id: 'plate_edge_t2', positions: [5, -5, 4, 5, 5, 4], role: 'unchanged' },
    { id: 'plate_edge_t3', positions: [5, 5, 4, -5, 5, 4], role: 'unchanged' },
    { id: 'plate_edge_t4', positions: [-5, 5, 4, -5, -5, 4], role: 'unchanged' },
  ],
};

// 5. Boolean Subtract After (Plate with central cylindrical hole)
export const MESH_BOOLEAN_AFTER_GM: MeshData = {
  schemaVersion: 1,
  bodyId: 'body_bool_after_gm',
  source: {
    kernel: 'GM Kernel',
    geometryAuthor: 'Boolean::Subtract',
    tessellator: 'PK_TOPOL_render_facet',
    commit: 'a1e4500',
    chordTolerance: 0.001,
  },
  faces: [
    { id: 'plate_bottom_mod', ...makeQuad([-5, -5, 0], [5, -5, 0], [5, 5, 0], [-5, 5, 0]), role: 'modified' },
    { id: 'plate_top_mod', ...makeQuad([-5, -5, 4], [-5, 5, 4], [5, 5, 4], [5, -5, 4]), role: 'modified' },
    { id: 'plate_front', ...makeQuad([-5, -5, 0], [-5, -5, 4], [5, -5, 4], [5, -5, 0]), role: 'unchanged' },
    { id: 'plate_back', ...makeQuad([-5, 5, 0], [5, 5, 0], [5, 5, 4], [-5, 5, 4]), role: 'unchanged' },
    { id: 'plate_left', ...makeQuad([-5, -5, 0], [-5, 5, 0], [-5, 5, 4], [-5, -5, 4]), role: 'unchanged' },
    { id: 'plate_right', ...makeQuad([5, -5, 0], [5, -5, 4], [5, 5, 4], [5, 5, 0]), role: 'unchanged' },
    // Cylindrical bore hole inner surface (R=2.5, H=4)
    {
      id: 'inner_hole_cylinder_01',
      positions: [
        2.5, 0, 0,  2.5, 0, 4,  0, 2.5, 4,  0, 2.5, 0,
        0, 2.5, 0,  0, 2.5, 4, -2.5, 0, 4, -2.5, 0, 0,
        -2.5, 0, 0, -2.5, 0, 4,  0, -2.5, 4, 0, -2.5, 0,
        0, -2.5, 0, 0, -2.5, 4,  2.5, 0, 4,  2.5, 0, 0,
      ],
      indices: [
        0, 1, 2, 0, 2, 3,
        4, 5, 6, 4, 6, 7,
        8, 9, 10, 8, 10, 11,
        12, 13, 14, 12, 14, 15,
      ],
      role: 'newBlend',
    },
  ],
  edges: [
    { id: 'hole_top_loop', positions: [2.5, 0, 4, 0, 2.5, 4, 0, 2.5, 4, -2.5, 0, 4, -2.5, 0, 4, 0, -2.5, 4, 0, -2.5, 4, 2.5, 0, 4], role: 'newBlend' },
    { id: 'hole_bottom_loop', positions: [2.5, 0, 0, 0, 2.5, 0, 0, 2.5, 0, -2.5, 0, 0, -2.5, 0, 0, 0, -2.5, 0, 0, -2.5, 0, 2.5, 0, 0], role: 'newBlend' },
  ],
};

export const MESH_BOOLEAN_AFTER_PK: MeshData = {
  ...MESH_BOOLEAN_AFTER_GM,
  bodyId: 'body_bool_after_pk',
  source: {
    kernel: 'Parasolid v38.0',
    geometryAuthor: 'PK_BODY_boolean_2',
    tessellator: 'PK_TOPOL_render_facet',
    commit: 'Parasolid-v38.0.165',
    chordTolerance: 0.001,
  },
};

// Fixture Manifest Data
export const DEV_FIXTURE_MANIFEST: ManifestData = {
  schemaVersion: 1,
  run: {
    id: 'run-dev-fixture-20261111',
    commit: 'a1e4500 (HEAD -> main)',
    generatedAt: '2026-11-11T01:30:00Z',
    pkVersion: 'Parasolid v38.0.165',
    host: 'qingyu2 (Ubuntu 24.04 Linux 7.0.0-42)',
    configuration: 'Release NativeAOT / x86_64 / AVX2',
    scope: 'Blend Rolling Ball & Boolean Engine Regression Suite',
    provenance: 'GM Kernel Geometry with Parasolid Tessellation Oracle',
  },
  cases: [
    {
      id: 'case_blend_constant_radius_box',
      title: '立方体定半径滚球倒圆角 (R=2.0)',
      family: 'blend',
      operation: 'PK_BODY_fix_blends',
      status: 'pass',
      parameters: {
        radius: 2.0,
        blendType: 'PK_blend_type_rolling_ball',
        propagate: 'smooth',
        chordTolerance: 0.0005,
      },
      apis: {
        gm: {
          name: 'GmBlendEngine.FixBlends',
          parameters: { targetEdge: 'edge_blend_target', radius: 2.0, method: 'RollingBall' },
        },
        pk: {
          name: 'PK_BODY_fix_blends',
          parameters: { n_edges: 1, blend_type: 'rolling_ball', radius: 2.0 },
        },
      },
      steps: [
        {
          id: 'step_1_init',
          title: '步骤 1: 初始实体与目标边标注',
          gm: { before: 'meshes/box_before.json', after: null },
          pk: { before: 'meshes/box_before.json', after: null },
        },
        {
          id: 'step_2_blended',
          title: '步骤 2: 滚球圆角曲面生成与裁剪收敛',
          gm: { before: 'meshes/box_before.json', after: 'meshes/box_after_gm.json' },
          pk: { before: 'meshes/box_before.json', after: 'meshes/box_after_pk.json' },
        },
      ],
      checks: [
        {
          id: 'chk_topology_faces',
          label: '实体面数量 (Face Count)',
          status: 'pass',
          gm: 7,
          pk: 7,
          absDelta: 0,
          normalizedDelta: 0,
          threshold: 0,
          unit: 'count',
          evidence: 'evidence/topol_face_match.txt',
          note: 'GM 与 PK 产生相同数量的拓扑面 (含新生成的圆角面)',
        },
        {
          id: 'chk_topology_edges',
          label: '拓扑边数量 (Edge Count)',
          status: 'pass',
          gm: 14,
          pk: 14,
          absDelta: 0,
          normalizedDelta: 0,
          threshold: 0,
          unit: 'count',
          evidence: null,
          note: '边拓扑完全对应',
        },
        {
          id: 'chk_max_surface_deviation',
          label: '圆角曲面最大几何偏差 (Max Deviation)',
          status: 'pass',
          gm: 0.00012,
          pk: 0.0,
          absDelta: 0.00012,
          normalizedDelta: 0.00006,
          threshold: 0.0005,
          unit: 'mm',
          evidence: 'evidence/blend_deviation_chart.png',
          note: '曲面网格采样在容差 0.0005mm 以内，严格通过',
        },
        {
          id: 'chk_body_compare_global',
          label: 'PK_DEBUG_BODY_compare 全局结果',
          status: 'pass',
          gm: 'PK_BODY_COMPARE_diff_none_c',
          pk: 'PK_BODY_COMPARE_diff_none_c',
          absDelta: null,
          normalizedDelta: null,
          threshold: null,
          unit: 'enum',
          evidence: 'evidence/pk_debug_body_compare.log',
          note: 'Parasolid 官方比较器返回无差异',
        },
      ],
      performance: {
        gmMs: 0.42,
        pkMs: 0.61,
        ratioGmOverPk: 0.69,
        samples: 50,
        warmup: 5,
        scope: 'Cold/Warm Mixed 50 runs',
        correctnessQualified: true,
        host: 'qingyu2 AMD Ryzen / x86_64',
        configuration: 'Release - .NET 10 NativeAOT',
        note: 'GM 滚球初边搜索算法较快，耗时优于 PK 约 31%',
      },
      evidence: [
        { label: '拓扑对照清单 (topol_face_match.txt)', path: 'evidence/topol_face_match.txt' },
        { label: '几何偏差分布图 (blend_deviation.png)', path: 'evidence/blend_deviation_chart.png' },
        { label: 'PK 对比日志 (pk_debug_body_compare.log)', path: 'evidence/pk_debug_body_compare.log' },
      ],
      notes: [
        '此 Case 用于验证定半径单凸边滚球圆角生成与拓扑重构。',
        '网格采用 Parasolid 官方 Oracle 离散生成，保留真实几何特征。',
      ],
      source: {
        path: 'tests/KernelTests/Blend/ConstantRadiusBoxTests.cs',
        revision: 'a1e4500',
      },
    },
    {
      id: 'case_boolean_cylinder_subtract',
      title: '基板打孔布尔减运算 (Cylinder Hole Subtract)',
      family: 'boolean',
      operation: 'PK_BODY_boolean_2',
      status: 'pass',
      parameters: {
        operationType: 'PK_boolean_difference',
        holeRadius: 2.5,
        holeDepth: 4.0,
      },
      apis: {
        gm: {
          name: 'GmBooleanEngine.Subtract',
          parameters: { target: 'plate_body', tool: 'cylinder_tool' },
        },
        pk: {
          name: 'PK_BODY_boolean_2',
          parameters: { target: 1001, tool: 1002, op: 'PK_boolean_difference' },
        },
      },
      steps: [
        {
          id: 'step_1_boolean_pre',
          title: '步骤 1: 待减基板与刀具圆柱相交前态',
          gm: { before: 'meshes/bool_before.json', after: null },
          pk: { before: 'meshes/bool_before.json', after: null },
        },
        {
          id: 'step_2_boolean_done',
          title: '步骤 2: 布尔差集计算与内孔圆柱生成',
          gm: { before: 'meshes/bool_before.json', after: 'meshes/bool_after_gm.json' },
          pk: { before: 'meshes/bool_before.json', after: 'meshes/bool_after_pk.json' },
        },
      ],
      checks: [
        {
          id: 'chk_mass_properties_volume',
          label: '实体体积 (Volume)',
          status: 'pass',
          gm: 321.4601,
          pk: 321.4602,
          absDelta: 0.0001,
          normalizedDelta: 3.1e-7,
          threshold: 0.001,
          unit: 'mm³',
          evidence: null,
          note: '体积差异处于数值计算精度容限内',
        },
        {
          id: 'chk_topology_faces_bool',
          label: '面总数 (Faces)',
          status: 'pass',
          gm: 7,
          pk: 7,
          absDelta: 0,
          normalizedDelta: 0,
          threshold: 0,
          unit: 'count',
          evidence: null,
          note: '内外环拓扑匹配成功',
        },
      ],
      performance: {
        gmMs: 1.18,
        pkMs: 0.95,
        ratioGmOverPk: 1.24,
        samples: 30,
        warmup: 3,
        scope: 'Benchmarked on qingyu2',
        correctnessQualified: true,
        host: 'qingyu2 AMD Ryzen / x86_64',
        configuration: 'Release - .NET 10 NativeAOT',
        note: 'PK 在圆柱相交闭环求交器上具有优化管线，目前 PK 快约 24%',
      },
      evidence: [
        { label: '布尔求交曲线报告 (ssi_curves.json)', path: 'evidence/ssi_curves.json' },
      ],
      notes: [
        '验证柱面与平面的四向修剪算法与内外环拓扑分裂。',
      ],
      source: {
        path: 'tests/KernelTests/Boolean/CylinderHoleSubtractTests.cs',
        revision: 'a1e4500',
      },
    },
    {
      id: 'case_blend_chamfer_fail',
      title: '多边汇交非对称斜角倒角 (Corner Blend)',
      family: 'blend',
      operation: 'PK_BODY_fix_blends',
      status: 'fail',
      parameters: {
        edgeCount: 3,
        setbackDistance: [1.5, 2.0, 2.5],
        cornerType: 'spherical_mitre',
      },
      apis: {
        gm: {
          name: 'GmBlendEngine.ChamferMitreCorner',
          parameters: { cornerVertex: 'v_corner_01', setback: [1.5, 2.0, 2.5] },
        },
        pk: {
          name: 'PK_BODY_fix_blends',
          parameters: { n_edges: 3, type: 'corner_setback' },
        },
      },
      steps: [
        {
          id: 'step_1_corner_init',
          title: '步骤 1: 待倒角三边汇交顶点',
          gm: { before: 'meshes/box_before.json', after: null },
          pk: { before: 'meshes/box_before.json', after: null },
        },
        {
          id: 'step_2_corner_blended',
          title: '步骤 2: 角点过渡面构建',
          gm: { before: 'meshes/box_before.json', after: 'meshes/box_after_gm.json' },
          pk: { before: 'meshes/box_before.json', after: 'meshes/box_after_pk.json' },
        },
      ],
      checks: [
        {
          id: 'chk_corner_continuity',
          label: '过渡面切矢 G1 连续性 (G1 Continuity)',
          status: 'fail',
          gm: 3.42,
          pk: 0.05,
          absDelta: 3.37,
          normalizedDelta: 67.4,
          threshold: 0.5,
          unit: 'deg',
          evidence: 'evidence/continuity_defect.png',
          note: 'GM 角点在三阶曲面拼接处切矢角度偏差达到 3.42°，超过 0.5° 门限要求',
        },
        {
          id: 'chk_body_compare_diff',
          label: 'PK_DEBUG_BODY_compare 结果',
          status: 'fail',
          gm: 'PK_BODY_COMPARE_diff_local_c',
          pk: 'PK_BODY_COMPARE_diff_none_c',
          absDelta: null,
          normalizedDelta: null,
          threshold: null,
          unit: 'enum',
          evidence: 'evidence/corner_debug.log',
          note: '局部面匹配失败，face_corner_blend 未与基准配对',
        },
      ],
      performance: {
        gmMs: 2.85,
        pkMs: 1.12,
        ratioGmOverPk: 2.54,
        samples: 20,
        warmup: 2,
        scope: 'Single thread benchmark',
        correctnessQualified: false,
        host: 'qingyu2 AMD Ryzen',
        configuration: 'Release - .NET 10',
        note: '因 G1 连续性未达标，此项性能成绩不具备正确性资格 (correctnessQualified=false)',
      },
      evidence: [
        { label: '曲率缺陷渲染图 (continuity_defect.png)', path: 'evidence/continuity_defect.png' },
        { label: '调试日志 (corner_debug.log)', path: 'evidence/corner_debug.log' },
      ],
      notes: [
        '【已知缺陷】：三边汇交处多阶混合曲面法向约束方程目前采用简单代数逼近，需引入 Parasolid 风格的五次 Bezier 角片补丁。',
      ],
      source: {
        path: 'tests/KernelTests/Blend/MitreCornerTests.cs',
        revision: 'a1e4500',
      },
    },
    {
      id: 'case_blend_missing_pk',
      title: '复杂导轨截面可变半径倒圆角 (PK 数据未就绪)',
      family: 'blend',
      operation: 'PK_BODY_fix_blends',
      status: 'missing',
      parameters: {
        lawType: 'cubic_variable',
        startRadius: 1.0,
        endRadius: 3.5,
      },
      apis: {
        gm: {
          name: 'GmBlendEngine.VariableRadiusBlend',
          parameters: { startR: 1.0, endR: 3.5, curveLaw: 'cubic' },
        },
        pk: {
          name: 'PK_BODY_fix_blends',
          parameters: { blend_law: 'PK_blend_law_cubic' },
        },
      },
      steps: [
        {
          id: 'step_1_guide_init',
          title: '步骤 1: 导轨初始截面',
          gm: { before: 'meshes/box_before.json', after: null },
          pk: { before: null, after: null },
        },
        {
          id: 'step_2_guide_variable',
          title: '步骤 2: 变半径过渡几何',
          gm: { before: 'meshes/box_before.json', after: 'meshes/box_after_gm.json' },
          pk: { before: null, after: null },
        },
      ],
      checks: [
        {
          id: 'chk_pk_oracle_presence',
          label: 'PK Oracle 基准对照数据',
          status: 'missing',
          gm: 'Available',
          pk: null,
          absDelta: null,
          normalizedDelta: null,
          threshold: null,
          unit: 'status',
          evidence: null,
          note: 'Mac Worker 尚未导出本测试例的 Parasolid Oracle 几何与指标',
        },
      ],
      performance: {
        gmMs: 1.45,
        pkMs: null,
        ratioGmOverPk: null,
        samples: 10,
        warmup: 2,
        scope: 'Preliminary GM Run',
        correctnessQualified: false,
        host: 'qingyu2',
        configuration: 'Release',
        note: 'PK 耗时缺失，无法计算 GM÷PK 耗时倍率',
      },
      evidence: [],
      notes: [
        '等待并行 Mac Worker (ctx_391ece96108a) 输出 Parasolid 参照数据。',
      ],
      source: {
        path: 'tests/KernelTests/Blend/VariableRadiusTests.cs',
        revision: 'a1e4500',
      },
    },
    {
      id: 'case_boolean_unsupported',
      title: '高维退化相切非流形布尔交 (Degenerate Tangent Intersection)',
      family: 'boolean',
      operation: 'PK_BODY_boolean_2',
      status: 'unsupported',
      parameters: {
        topologyType: 'non_manifold_tangent_contact',
        singularities: 4,
      },
      apis: {
        gm: {
          name: 'GmBooleanEngine.Intersect',
          parameters: { allowNonManifold: false },
        },
        pk: {
          name: 'PK_BODY_boolean_2',
          parameters: { op: 'PK_boolean_intersect' },
        },
      },
      steps: [
        {
          id: 'step_1_init',
          title: '步骤 1: 奇异相切待求交实体',
          gm: { before: null, after: null },
          pk: { before: null, after: null },
        },
      ],
      checks: [
        {
          id: 'chk_support_status',
          label: '退化相切求交器支持状态',
          status: 'unsupported',
          gm: 'UnsupportedKernelOpException',
          pk: 'PK_ERROR_not_implemented',
          absDelta: null,
          normalizedDelta: null,
          threshold: null,
          unit: 'exception',
          evidence: null,
          note: '当前内核版本未开启高阶奇异相切布尔支持',
        },
      ],
      performance: {
        gmMs: null,
        pkMs: null,
        ratioGmOverPk: null,
        note: '不支持用例不进行性能采集',
      },
      evidence: [],
      notes: [
        '属于未来架构规划支持范围 (Epic: Non-manifold Boolean & Degenerate SSI)。',
      ],
      source: {
        path: 'tests/KernelTests/Boolean/NonManifoldTests.cs',
        revision: 'a1e4500',
      },
    },
  ],
};

export const DEV_FIXTURE_INDEX: IndexData = {
  schemaVersion: 1,
  generatedAt: '2026-11-11T01:30:00Z',
  latestRunId: 'run-dev-fixture-20261111',
  runs: [
    {
      id: 'run-dev-fixture-20261111',
      commit: 'a1e4500',
      generatedAt: '2026-11-11T01:30:00Z',
      label: 'Dev Fixture: 演示测试集 (Blend & Boolean)',
      manifest: 'runs/run-dev-fixture-20261111/manifest.json',
    },
  ],
};

// Map of fixture meshes for offline dev simulation
export const DEV_FIXTURE_MESHES: Record<string, MeshData> = {
  'meshes/box_before.json': MESH_BOX_BEFORE,
  'meshes/box_after_gm.json': MESH_BOX_AFTER_GM,
  'meshes/box_after_pk.json': MESH_BOX_AFTER_PK,
  'meshes/bool_before.json': MESH_BOOLEAN_BEFORE,
  'meshes/bool_after_gm.json': MESH_BOOLEAN_AFTER_GM,
  'meshes/bool_after_pk.json': MESH_BOOLEAN_AFTER_PK,
};
