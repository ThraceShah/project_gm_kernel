#include <assert.h>
#include <stdio.h>
#include "parasolid_kernel.h"

int main(void)
{
    PK_SESSION_start_o_t options;
    PK_SESSION_start_o_m(options);

    PK_ERROR_code_t error = PK_SESSION_start(&options);
    assert(error == 0);

    PK_POINT_sf_t point_sf;
    point_sf.position.coord[0] = 1.0;
    point_sf.position.coord[1] = 2.0;
    point_sf.position.coord[2] = 3.0;

    PK_POINT_t point = PK_ENTITY_null;
    error = PK_POINT_create(&point_sf, &point);
    assert(error == 0);
    assert(point != PK_ENTITY_null);

    PK_CLASS_t entity_class = PK_CLASS_null;
    error = PK_ENTITY_ask_class(point, &entity_class);
    assert(error == 0);
    assert(entity_class == PK_CLASS_point);

    error = PK_SESSION_stop();
    assert(error == 0);

    printf("abi smoke ok, point tag=%d class=%d\n", point, entity_class);
    return 0;
}
