#ifndef PROJECT_GM_DISPLAY_H
#define PROJECT_GM_DISPLAY_H
#include "parasolid_kernel.h"
#ifdef __cplusplus
extern "C" {
#endif

/* ABI extension: persistent identifiers are scoped to this one XT part. */
typedef struct PGM_DISPLAY_binding_s {
    PK_ENTITY_t entity;
    PK_CLASS_t entity_class;
    int identifier;
    int generation;
} PGM_DISPLAY_binding_t;

typedef struct PGM_DISPLAY_snapshot_s {
    int session_generation;
    PK_BODY_t body;
    int body_generation;
    int n_bindings;
    PGM_DISPLAY_binding_t *bindings;
    PK_MEMORY_block_t xt;
} PGM_DISPLAY_snapshot_t;

PK_ERROR_code_t PGM_BODY_export_display(PK_BODY_t, PGM_DISPLAY_snapshot_t *);
PK_ERROR_code_t PGM_DISPLAY_snapshot_free(PGM_DISPLAY_snapshot_t *);
PK_ERROR_code_t PGM_DISPLAY_validate_pick(int session_generation, PK_BODY_t body,
    int body_generation, PK_ENTITY_t entity, int entity_generation,
    PK_CLASS_t entity_class, PK_LOGICAL_t *valid);

#ifdef __cplusplus
}
#endif
#endif
