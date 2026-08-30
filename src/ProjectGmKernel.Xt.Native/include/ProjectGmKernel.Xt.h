#ifndef PROJECT_GM_KERNEL_XT_H
#define PROJECT_GM_KERNEL_XT_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#define PGM_XT_API __declspec(dllimport)
#else
#define PGM_XT_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define PGM_XT_ABI_VERSION 1u
typedef uint64_t PGM_XT_context_t;
typedef uint64_t PGM_XT_document_t;
typedef uint64_t PGM_XT_brep_t;
typedef int32_t PGM_XT_status_t;

typedef struct PGM_XT_context_o_s {
    uint32_t struct_size;
    uint32_t version;
    const char *schema_directory_utf8;
    uint32_t flags;
    int32_t maximum_cached_schemas;
} PGM_XT_context_o_t;

typedef struct PGM_XT_buffer_s { void *data; size_t size; } PGM_XT_buffer_t;
typedef struct PGM_XT_range_s { int32_t offset; int32_t count; } PGM_XT_range_t;
typedef struct PGM_XT_vector3_s { double x, y, z; } PGM_XT_vector3_t;

typedef struct PGM_XT_part_row_s { uint8_t kind; int32_t transmit_index; uint8_t is_root,has_external_next,has_external_previous; int32_t external_next,external_previous,body,assembly,name,next,previous; } PGM_XT_part_row_t;
typedef struct PGM_XT_body_row_s { uint8_t kind; int32_t highest_node_id,lowest_node_id,reference_instance,owner,child,surface,curve,point,boundary_surface,boundary_curve,boundary_point,boundary_mesh,boundary_polyline; PGM_XT_range_t regions,shells,edges,vertices,attributes; } PGM_XT_body_row_t;
typedef struct PGM_XT_region_row_s { int32_t persistent_id,transmit_index,body,owner_body,first_shell; uint8_t is_solid; int32_t next,previous; } PGM_XT_region_row_t;
typedef struct PGM_XT_shell_row_s { int32_t persistent_id,transmit_index,body,region,first_face,front_face,first_edge,first_vertex,next; } PGM_XT_shell_row_t;
typedef struct PGM_XT_face_row_s { int32_t persistent_id,transmit_index,shell,first_loop,surface; uint8_t surface_kind; int32_t surface_entity,next,previous,next_front,previous_front,next_on_surface,previous_on_surface,front_shell; int8_t sense; } PGM_XT_face_row_t;
typedef struct PGM_XT_loop_row_s { int32_t persistent_id,transmit_index,face,first_fin,next; uint8_t is_outer; } PGM_XT_loop_row_t;
typedef struct PGM_XT_fin_row_s { int32_t persistent_id,transmit_index,loop,edge,vertex,curve; uint8_t curve_kind; int32_t curve_entity,forward,backward,other,next_at_vertex; int8_t sense; uint8_t sense_present; } PGM_XT_fin_row_t;
typedef struct PGM_XT_edge_row_s { int32_t persistent_id,transmit_index,start,end,curve; uint8_t curve_kind; int32_t curve_entity,first_fin,next,previous,next_on_curve,previous_on_curve,body; uint8_t owner_kind; int32_t owner; double tolerance; uint8_t tolerance_present; } PGM_XT_edge_row_t;
typedef struct PGM_XT_vertex_row_s { int32_t persistent_id,transmit_index,point,first_fin,first_edge,next,previous,body; uint8_t owner_kind; int32_t owner; double tolerance; uint8_t tolerance_present; } PGM_XT_vertex_row_t;
typedef struct PGM_XT_point_row_s { int32_t persistent_id,transmit_index; PGM_XT_vector3_t position; uint8_t owner_kind; int32_t owner,next,previous; } PGM_XT_point_row_t;
typedef struct PGM_XT_geometry_row_s {
    uint8_t kind; int32_t persistent_id,transmit_index; PGM_XT_vector3_t origin,axis,reference; double a,b,c,d,e,scale;
    PGM_XT_range_t control_points, weights, knots, multiplicities,knots_u,knots_v,multiplicities_u,multiplicities_v;
    int32_t basis, profile, spine, chart, start_limit, end_limit, intersection_data,geometric_owner; uint8_t owner_kind; int32_t owner, next, previous; uint16_t subtype; int8_t sense; uint8_t flag;
    int16_t degree_u, degree_v; int32_t control_count_u,control_count_v; int16_t vertex_dimension; uint32_t knot_type_u,knot_type_v; uint8_t rational, periodic_u, periodic_v,closed_u,closed_v, form;
    double original_u_low,original_u_high,original_v_low,original_v_high,extended_u_low,extended_u_high,extended_v_low,extended_v_high; uint8_t self_intersection,scale_present; uint16_t original_u_start,original_u_end,original_v_start,original_v_end,extended_u_start,extended_u_end,extended_v_start,extended_v_end;
} PGM_XT_geometry_row_t;
typedef struct PGM_XT_transform_row_s { int32_t transmit_index; double m00,m01,m02,m03,m10,m11,m12,m13,m20,m21,m22,m23; int32_t owner; double scale; int32_t flag; } PGM_XT_transform_row_t;
typedef struct PGM_XT_frame_row_s { int32_t persistent_id,transmit_index,geometry; uint8_t geometry_kind; int32_t geometry_entity,next,previous,next_on_geometry,previous_on_geometry; uint8_t owner_kind; int32_t owner; int8_t sense; } PGM_XT_frame_row_t;
typedef struct PGM_XT_assembly_row_s { int32_t highest_node_id; PGM_XT_range_t instances,attributes; int32_t name,reference_instance,next,previous; } PGM_XT_assembly_row_t;
typedef struct PGM_XT_instance_row_s { int32_t transmit_index,owner,part; uint8_t has_external_part,external_links; int32_t transform,next_in_part,previous_in_part,next_of_part,previous_of_part; PGM_XT_range_t attributes; } PGM_XT_instance_row_t;
typedef struct PGM_XT_attribute_definition_row_s { int32_t transmit_index,name; PGM_XT_range_t field_definitions; uint64_t legal_owners,actions; int32_t type_id; uint8_t built_in; } PGM_XT_attribute_definition_row_t;
typedef struct PGM_XT_attribute_field_definition_row_s { int32_t name; uint8_t kind; int32_t cardinality; } PGM_XT_attribute_field_definition_row_t;
typedef struct PGM_XT_attribute_row_s { int32_t transmit_index,definition; uint8_t owner_kind; int32_t owner; PGM_XT_range_t values; } PGM_XT_attribute_row_t;
typedef struct PGM_XT_attribute_value_row_s { uint8_t kind; int64_t integer; double real; PGM_XT_vector3_t vector; PGM_XT_range_t data; int32_t entity; uint8_t present; } PGM_XT_attribute_value_row_t;
typedef struct PGM_XT_user_field_row_s { uint8_t owner_kind; int32_t owner; PGM_XT_range_t payload; } PGM_XT_user_field_row_t;
typedef struct PGM_XT_mesh_row_s { uint8_t kind; int32_t persistent_id,transmit_index; uint8_t owner_kind; int32_t owner,next,previous,data,psm_mesh,position_pool,normal_pool,position_indices,normal_indices; PGM_XT_range_t references,values,vertices,topology,facets,fins; uint16_t sense; uint32_t token,token2; int32_t i0,i1,i2,i3,i4; double d0,d1; uint8_t logical; } PGM_XT_mesh_row_t;
typedef struct PGM_XT_lattice_row_s { uint8_t owner_kind; int32_t owner; PGM_XT_range_t vertices,rods,balls,ijk_boxes; } PGM_XT_lattice_row_t;
typedef struct PGM_XT_connection_row_s { int32_t a,b,c,d,next; uint32_t flags; } PGM_XT_connection_row_t;
typedef struct PGM_XT_hvector_row_s { double x,y,z,w; } PGM_XT_hvector_row_t;
typedef struct PGM_XT_chart_row_s { int32_t transmit_index; double base_parameter,base_scale; int32_t chart_count; double chordal_error,angular_error,parameter_error_low,parameter_error_high; uint8_t parameter_error_low_present,parameter_error_high_present; PGM_XT_range_t hvectors; } PGM_XT_chart_row_t;
typedef struct PGM_XT_limit_row_s { int32_t transmit_index; uint16_t type,term_use; uint8_t type_present,term_use_present; PGM_XT_range_t hvectors; } PGM_XT_limit_row_t;
typedef struct PGM_XT_intersection_data_row_s { int32_t transmit_index; uint32_t uv_type; PGM_XT_range_t values; } PGM_XT_intersection_data_row_t;
typedef struct PGM_XT_geometric_owner_row_s { int32_t transmit_index; uint8_t owner_kind; int32_t owner,next,previous; uint8_t shared_geometry_kind; int32_t shared_geometry; } PGM_XT_geometric_owner_row_t;
typedef struct PGM_XT_string_row_s { int32_t offset,byte_count; } PGM_XT_string_row_t;
typedef struct PGM_XT_scalar_row_s { double value; uint8_t present; } PGM_XT_scalar_row_t;
typedef struct PGM_XT_transmit_order_row_s { int32_t node_index,order; } PGM_XT_transmit_order_row_t;

typedef struct PGM_XT_brep_counts_s {
    int32_t parts, bodies, regions, shells, faces, loops, fins, edges, vertices;
    int32_t points, geometries, transforms, frames, assemblies, instances;
    int32_t attribute_definitions, attribute_field_definitions, attributes, attribute_values;
    int32_t user_fields, meshes, lattices, connections, control_points, scalars, charts, limits, intersection_data, hvectors, geometric_owners, transmit_order, strings, payload_bytes;
} PGM_XT_brep_counts_t;

typedef enum PGM_XT_table_kind_e {
    PGM_XT_TABLE_parts = 0, PGM_XT_TABLE_bodies, PGM_XT_TABLE_regions,
    PGM_XT_TABLE_shells, PGM_XT_TABLE_faces, PGM_XT_TABLE_loops,
    PGM_XT_TABLE_fins, PGM_XT_TABLE_edges, PGM_XT_TABLE_vertices,
    PGM_XT_TABLE_points, PGM_XT_TABLE_geometries, PGM_XT_TABLE_transforms,
    PGM_XT_TABLE_frames, PGM_XT_TABLE_assemblies, PGM_XT_TABLE_instances,
    PGM_XT_TABLE_attribute_definitions, PGM_XT_TABLE_attribute_field_definitions,
    PGM_XT_TABLE_attributes, PGM_XT_TABLE_attribute_values, PGM_XT_TABLE_user_fields,
    PGM_XT_TABLE_meshes, PGM_XT_TABLE_lattices, PGM_XT_TABLE_connections,
    PGM_XT_TABLE_control_points, PGM_XT_TABLE_scalars, PGM_XT_TABLE_charts,
    PGM_XT_TABLE_limits, PGM_XT_TABLE_intersection_data, PGM_XT_TABLE_hvectors, PGM_XT_TABLE_geometric_owners, PGM_XT_TABLE_transmit_order, PGM_XT_TABLE_strings,
    PGM_XT_TABLE_payload
} PGM_XT_table_kind_t;

/* Context copies the directory string. Directory contents must remain stable. */
PGM_XT_API PGM_XT_status_t PGM_XT_CONTEXT_create(const PGM_XT_context_o_t *, PGM_XT_context_t *);
PGM_XT_API PGM_XT_status_t PGM_XT_CONTEXT_load_all(PGM_XT_context_t);
PGM_XT_API PGM_XT_status_t PGM_XT_CONTEXT_delete(PGM_XT_context_t);

PGM_XT_API PGM_XT_status_t PGM_XT_DOCUMENT_read(PGM_XT_context_t, const uint8_t *, size_t, PGM_XT_document_t *);
PGM_XT_API PGM_XT_status_t PGM_XT_DOCUMENT_write(PGM_XT_context_t, PGM_XT_document_t, const char *target_schema_identity_utf8, PGM_XT_buffer_t *);
PGM_XT_API PGM_XT_status_t PGM_XT_DOCUMENT_delete(PGM_XT_document_t);
PGM_XT_API PGM_XT_status_t PGM_XT_DOCUMENT_to_brep(PGM_XT_document_t, PGM_XT_brep_t *);
PGM_XT_API PGM_XT_status_t PGM_XT_BREP_to_document(PGM_XT_context_t, PGM_XT_brep_t, const char *target_schema_identity_utf8, PGM_XT_document_t *);

PGM_XT_API PGM_XT_status_t PGM_XT_BREP_create(const PGM_XT_brep_counts_t *, PGM_XT_brep_t *);
PGM_XT_API PGM_XT_status_t PGM_XT_BREP_get_counts(PGM_XT_brep_t, PGM_XT_brep_counts_t *);
PGM_XT_API PGM_XT_status_t PGM_XT_BREP_get_table_view(PGM_XT_brep_t, int32_t table_kind, void **data, int32_t *count, int32_t *stride, uint8_t *read_only);
PGM_XT_API PGM_XT_status_t PGM_XT_BREP_finalize(PGM_XT_brep_t);
PGM_XT_API PGM_XT_status_t PGM_XT_BREP_validate(PGM_XT_brep_t);
PGM_XT_API PGM_XT_status_t PGM_XT_BREP_delete(PGM_XT_brep_t);

/* Standalone buffers returned by this library are released here. Table views
   are borrowed and remain valid until their owning B-rep handle is deleted. */
PGM_XT_API void PGM_XT_BUFFER_free(void *);
PGM_XT_API PGM_XT_status_t PGM_XT_ERROR_get(PGM_XT_buffer_t *);

#ifdef __cplusplus
}
#endif
#endif
