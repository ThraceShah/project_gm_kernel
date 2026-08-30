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
typedef uint64_t PGM_XT_model_t;
typedef int32_t PGM_XT_status_t;

typedef enum PGM_XT_schema_field_state_e { PGM_XT_FIELD_unavailable=0, PGM_XT_FIELD_null=1, PGM_XT_FIELD_value=2 } PGM_XT_schema_field_state_t;
typedef struct PGM_XT_range_s { int32_t offset,count; } PGM_XT_range_t;
typedef struct PGM_XT_variable_range_s { uint8_t state; int32_t offset,count; } PGM_XT_variable_range_t;
typedef struct PGM_XT_schema_vector_s { double x,y,z; } PGM_XT_schema_vector_t;
typedef struct PGM_XT_schema_interval_s { double low,high; } PGM_XT_schema_interval_t;
typedef struct PGM_XT_schema_box_s { double x_low,x_high,y_low,y_high,z_low,z_high; } PGM_XT_schema_box_t;

typedef struct PGM_XT_field_p_s { uint8_t state; int32_t value; } PGM_XT_field_p_t;
typedef struct PGM_XT_field_d_s { uint8_t state; int64_t value; } PGM_XT_field_d_t;
typedef struct PGM_XT_field_n_s { uint8_t state; int64_t value; } PGM_XT_field_n_t;
typedef struct PGM_XT_field_w_s { uint8_t state; int64_t value; } PGM_XT_field_w_t;
typedef struct PGM_XT_field_t_s { uint8_t state; int64_t value; } PGM_XT_field_t_t;
typedef struct PGM_XT_field_q_s { uint8_t state; int64_t value; } PGM_XT_field_q_t;
typedef struct PGM_XT_field_u_s { uint8_t state; uint64_t value; } PGM_XT_field_u_t;
typedef struct PGM_XT_field_f_s { uint8_t state; double value; } PGM_XT_field_f_t;
typedef struct PGM_XT_field_c_s { uint8_t state,value; } PGM_XT_field_c_t;
typedef struct PGM_XT_field_l_s { uint8_t state,value; } PGM_XT_field_l_t;
typedef struct PGM_XT_field_v_s { uint8_t state; PGM_XT_schema_vector_t value; } PGM_XT_field_v_t;
typedef struct PGM_XT_field_h_s { uint8_t state; PGM_XT_schema_vector_t value; } PGM_XT_field_h_t;
typedef struct PGM_XT_field_i_s { uint8_t state; PGM_XT_schema_interval_t value; } PGM_XT_field_i_t;
typedef struct PGM_XT_field_b_s { uint8_t state; PGM_XT_schema_box_t value; } PGM_XT_field_b_t;

typedef struct PGM_XT_context_o_s { uint32_t struct_size,version; const char *schema_directory_utf8; uint32_t flags; int32_t maximum_cached_schemas; } PGM_XT_context_o_t;
typedef struct PGM_XT_buffer_s { void *data; size_t size; } PGM_XT_buffer_t;

PGM_XT_API PGM_XT_status_t PGM_XT_CONTEXT_create(const PGM_XT_context_o_t *,PGM_XT_context_t *);
PGM_XT_API PGM_XT_status_t PGM_XT_CONTEXT_load_all(PGM_XT_context_t);
PGM_XT_API PGM_XT_status_t PGM_XT_CONTEXT_delete(PGM_XT_context_t);
PGM_XT_API PGM_XT_status_t PGM_XT_DOCUMENT_read(PGM_XT_context_t,const uint8_t *,size_t,PGM_XT_document_t *);
PGM_XT_API PGM_XT_status_t PGM_XT_DOCUMENT_write(PGM_XT_context_t,PGM_XT_document_t,const char *,PGM_XT_buffer_t *);
PGM_XT_API PGM_XT_status_t PGM_XT_DOCUMENT_delete(PGM_XT_document_t);
PGM_XT_API void PGM_XT_BUFFER_free(void *);
PGM_XT_API PGM_XT_status_t PGM_XT_ERROR_get(PGM_XT_buffer_t *);

#include "ProjectGmKernel.Xt.Schema.generated.h"

#ifdef __cplusplus
}
#endif
#endif
