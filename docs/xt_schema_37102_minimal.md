# Parasolid XT schema 37102 minimal notes

This note locks the project x_t work to schema number `37102`.
The writer emits the plain text schema name `SCH_3701000_37102`, matching
Parasolid `PK_PART_transmit_b` output when `transmit_version = 371`.

Sources:

- `third_party/parasolid/schema/sch_37102.sch_txt` is the field-order source of truth.
- `docs/parasolid_online_docs/xt_index.html` is the XT reference entry point.
- `docs/parasolid_online_docs/chapters/xt_chap.03.html` explains logical layout and schema syntax.
- `docs/parasolid_online_docs/chapters/xt_chap.04.html` explains physical/text layout.
- `docs/parasolid_online_docs/chapters/xt_chap.05.html` explains model structure.
- `docs/parasolid_online_docs/chapters/xt_chap.06.html` explains node definitions.

## Rules

- Emit text transmit first. Do not implement binary, neutral, embedded schema, mesh schema, or user-field support in the first writer/reader slice.
- Use `transmit_version = 371` when asking real Parasolid to produce oracle x_t; the default `0` may emit embedded/effective schema data.
- Serialize only schema fields with transmit flag `1`.
- Preserve schema field order exactly.
- Serialize pointer fields as XT node indices, not runtime tags.
- Treat non-transmitted fields as Parasolid runtime state, not file data.
- Validate generated x_t with Parasolid receive/transmit before using our reader as proof.

## Minimal node set

Use `dotnet run scripts/ExtractXtSchema.cs -- --focus BODY,SHELL,FACE,LOOP,HALFEDGE,EDGE,VERTEX,REGION,POINT,CIRCLE,PLANE,CYLINDER` to print the exact field list from `sch_37102.sch_txt`.

The first x_t implementation slice should cover:

- `BODY` (`12`): single-body root node.
- `SHELL` (`13`): region-owned shell.
- `FACE` (`14`): shared face with back/front shell fields.
- `LOOP` (`15`): face boundary loop.
- `EDGE` (`16`): curve-bearing topology edge.
- `HALFEDGE` (`17`): XT halfedge, called fin at the PK API level.
- `VERTEX` (`18`): point-bearing vertex; solid cylinders may have zero.
- `REGION` (`19`): solid or void region.
- `POINT` (`29`): point geometry.
- `CIRCLE` (`31`): circular edge geometry.
- `PLANE` (`50`): cap face surface geometry.
- `CYLINDER` (`51`): side face surface geometry.

## Primitive checkpoints

- Solid block: `1 BODY`, `2 REGION`, `2 SHELL`, `6 FACE`, `12 EDGE`, `8 VERTEX`.
- Solid cylinder: `1 BODY`, `2 REGION`, `2 SHELL`, `3 FACE`, `2 EDGE`, `0 VERTEX`.
- Every solid primitive includes infinite void and solid regions.
- A shared face is represented with opposite shell usage/sense, not duplicated as two unrelated faces.
