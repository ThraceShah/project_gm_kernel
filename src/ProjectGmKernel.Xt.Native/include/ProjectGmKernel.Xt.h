#ifndef PROJECT_GM_KERNEL_XT_H
#define PROJECT_GM_KERNEL_XT_H

#include <stddef.h>
#include <stdint.h>
#include <math.h>

#if defined(_WIN32)
#define PGM_XT_API __declspec(dllimport)
#else
#define PGM_XT_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define PGM_XT_ABI_VERSION 2u
typedef uint64_t PGM_XT_context_t;
typedef uint64_t PGM_XT_document_t;
typedef uint64_t PGM_XT_model_t;
typedef int32_t PGM_XT_status_t;

/*
 * Schema rows carry strongly-typed members (PGM_XT ABI v2). A transmitted
 * field is null when its member equals the sentinel below for its kind;
 * members of fields a schema does not transmit are not maintained.
 */
typedef struct PGM_XT_range_s { int32_t offset,count; } PGM_XT_range_t;
typedef struct PGM_XT_schema_vector_s { double x,y,z; } PGM_XT_schema_vector_t;
typedef struct PGM_XT_schema_interval_s { double low,high; } PGM_XT_schema_interval_t;
typedef struct PGM_XT_schema_box_s { double x_low,x_high,y_low,y_high,z_low,z_high; } PGM_XT_schema_box_t;

#define PGM_XT_NULL_INDEX (-1)
#define PGM_XT_NULL_INTEGER ((int64_t)INT64_MIN)
#define PGM_XT_NULL_UNSIGNED ((uint64_t)UINT64_MAX)
#define PGM_XT_NULL_REAL ((double)NAN)
#define PGM_XT_NULL_CHAR ((uint8_t)0xFF)
#define PGM_XT_NULL_LOGICAL ((uint8_t)0xFF)
#define PGM_XT_INDEX_IS_NULL(value) ((value) < 0)
#define PGM_XT_INTEGER_IS_NULL(value) ((value) == PGM_XT_NULL_INTEGER)
#define PGM_XT_UNSIGNED_IS_NULL(value) ((value) == PGM_XT_NULL_UNSIGNED)
#define PGM_XT_REAL_IS_NULL(value) ((value) != (value))
#define PGM_XT_CHAR_IS_NULL(value) ((value) == PGM_XT_NULL_CHAR)
#define PGM_XT_LOGICAL_IS_NULL(value) ((value) == PGM_XT_NULL_LOGICAL)
#define PGM_XT_VECTOR_IS_NULL(value) (PGM_XT_REAL_IS_NULL((value).x))
#define PGM_XT_INTERVAL_IS_NULL(value) (PGM_XT_REAL_IS_NULL((value).low))
#define PGM_XT_BOX_IS_NULL(value) (PGM_XT_REAL_IS_NULL((value).x_low))

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

#ifdef __cplusplus
}
#endif
#endif
