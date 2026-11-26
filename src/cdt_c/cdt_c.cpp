// cdt_c: a C interface over CDT's Triangulation<double>, for CSharpCDT. It covers what
// PythonCDT binds, one function per member, named as PythonCDT names them.
//
// Each call that can fail returns 0 on success, or 1 with the message kept for
// cdt_last_error. Indices are CDT's 32-bit VertInd and TriInd. A set or list CDT returns is
// kept in the handle's buffer: the call returns how many uint32 values it holds, and
// cdt_copy_buffer copies them out.

#include <CDT.h>
#include <extras/VerifyTopology.h>

#include <cstdint>
#include <exception>
#include <string>
#include <vector>

#if defined(_WIN32)
#define CDT_C_API extern "C" __declspec(dllexport)
#else
#define CDT_C_API extern "C" __attribute__((visibility("default")))
#endif

namespace
{
typedef CDT::Triangulation<double> Triangulation;

struct Handle
{
    Triangulation cdt;
    std::string error;
    std::vector<uint32_t> buffer;

    Handle(
        CDT::VertexInsertionOrder::Enum order,
        CDT::IntersectingConstraintEdges::Enum intersecting,
        double minDistToConstraintEdge)
        : cdt(order, intersecting, minDistToConstraintEdge)
    {}
};

Handle* H(void* handle)
{
    return static_cast<Handle*>(handle);
}

template <typename F>
int32_t guard(void* handle, F f)
{
    try
    {
        f();
        H(handle)->error.clear();
        return 0;
    }
    catch(const std::exception& e)
    {
        H(handle)->error = e.what();
    }
    catch(...)
    {
        H(handle)->error = "Unknown error in CDT";
    }
    return 1;
}

std::vector<CDT::Edge> toEdges(const uint32_t* vertexPairs, int32_t count)
{
    std::vector<CDT::Edge> edges;
    edges.reserve(count);
    for(int32_t i = 0; i < count; ++i)
        edges.push_back(CDT::Edge(vertexPairs[2 * i], vertexPairs[2 * i + 1]));
    return edges;
}

CDT::TriIndUSet toSet(const uint32_t* triangles, int32_t count)
{
    return CDT::TriIndUSet(triangles, triangles + count);
}

template <typename Container>
int32_t keepIndices(void* handle, const Container& indices)
{
    std::vector<uint32_t>& buffer = H(handle)->buffer;
    buffer.assign(indices.begin(), indices.end());
    return static_cast<int32_t>(buffer.size());
}

int32_t keepEdges(void* handle, const CDT::EdgeVec& edges)
{
    std::vector<uint32_t>& buffer = H(handle)->buffer;
    buffer.clear();
    for(const CDT::Edge& e : edges)
    {
        buffer.push_back(e.v1());
        buffer.push_back(e.v2());
    }
    return static_cast<int32_t>(buffer.size());
}

CDT::V2d<double> v2d(const double* xy)
{
    return CDT::V2d<double>(xy[0], xy[1]);
}
} // namespace

// Lifetime and errors

CDT_C_API void* cdt_create(
    int32_t vertexInsertionOrder,
    int32_t intersectingConstraintEdges,
    double minDistToConstraintEdge)
{
    try
    {
        return new Handle(
            static_cast<CDT::VertexInsertionOrder::Enum>(vertexInsertionOrder),
            static_cast<CDT::IntersectingConstraintEdges::Enum>(intersectingConstraintEdges),
            minDistToConstraintEdge);
    }
    catch(...)
    {
        return nullptr;
    }
}

CDT_C_API void cdt_destroy(void* handle)
{
    delete H(handle);
}

CDT_C_API const char* cdt_last_error(void* handle)
{
    return H(handle)->error.c_str();
}

CDT_C_API void cdt_copy_buffer(void* handle, uint32_t* values)
{
    const std::vector<uint32_t>& buffer = H(handle)->buffer;
    std::copy(buffer.begin(), buffer.end(), values);
}

// Building

// xy holds count points as x, y pairs.
CDT_C_API int32_t cdt_insert_vertices(void* handle, const double* xy, int32_t count)
{
    struct XY
    {
        double x, y;
    };
    const XY* first = reinterpret_cast<const XY*>(xy);
    return guard(handle, [&] {
        H(handle)->cdt.insertVertices(
            first,
            first + count,
            [](const XY& v) { return v.x; },
            [](const XY& v) { return v.y; });
    });
}

// vertexPairs holds count edges as pairs of vertex indices.
CDT_C_API int32_t cdt_insert_edges(void* handle, const uint32_t* vertexPairs, int32_t count)
{
    return guard(handle, [&] { H(handle)->cdt.insertEdges(toEdges(vertexPairs, count)); });
}

CDT_C_API int32_t cdt_conform_to_edges(void* handle, const uint32_t* vertexPairs, int32_t count)
{
    return guard(handle, [&] { H(handle)->cdt.conformToEdges(toEdges(vertexPairs, count)); });
}

CDT_C_API int32_t cdt_erase_super_triangle(void* handle)
{
    return guard(handle, [&] { H(handle)->cdt.eraseSuperTriangle(); });
}

CDT_C_API int32_t cdt_erase_outer_triangles(void* handle)
{
    return guard(handle, [&] { H(handle)->cdt.eraseOuterTriangles(); });
}

CDT_C_API int32_t cdt_erase_outer_triangles_and_holes(void* handle)
{
    return guard(handle, [&] { H(handle)->cdt.eraseOuterTrianglesAndHoles(); });
}

CDT_C_API int32_t cdt_is_finalized(void* handle)
{
    return H(handle)->cdt.isFinalized() ? 1 : 0;
}

CDT_C_API int32_t cdt_remove_triangles(void* handle, const uint32_t* triangles, int32_t count)
{
    return guard(handle, [&] { H(handle)->cdt.removeTriangles(toSet(triangles, count)); });
}

CDT_C_API int32_t cdt_finalize_triangulation(
    void* handle,
    const uint32_t* removedTriangles,
    int32_t count)
{
    return guard(handle, [&] {
        H(handle)->cdt.finalizeTriangulation(toSet(removedTriangles, count));
    });
}

// Refining

// Refines until no triangle is bad by the criterion or maxVerticesToInsert are added. toErase,
// when count is not negative, holds triangles left as they are; the triangles that replace them
// are kept in the buffer, with the call returning their count in toEraseCount. unrefined gets
// Unrefined's six counts in order.
CDT_C_API int32_t cdt_refine_triangles(
    void* handle,
    uint32_t maxVerticesToInsert,
    int32_t refinementCriterion,
    double refinementThreshold,
    const uint32_t* toErase,
    int32_t count,
    double minEdgeLength,
    int32_t refinementOrder,
    uint64_t* unrefined,
    int32_t* toEraseCount)
{
    return guard(handle, [&] {
        CDT::TriIndUSet set;
        if(count >= 0)
            set = toSet(toErase, count);
        const CDT::Unrefined u = H(handle)->cdt.refineTriangles(
            maxVerticesToInsert,
            static_cast<CDT::RefinementCriterion::Enum>(refinementCriterion),
            refinementThreshold,
            count >= 0 ? &set : nullptr,
            minEdgeLength,
            static_cast<CDT::RefinementOrder::Enum>(refinementOrder));
        unrefined[0] = u.shortEdgeTriangles;
        unrefined[1] = u.circumcenterOutside;
        unrefined[2] = u.circumcenterOnVertex;
        unrefined[3] = u.sharpFixedCorner;
        unrefined[4] = u.shortEdges;
        unrefined[5] = u.splitVertexInvalid;
        *toEraseCount = count >= 0 ? keepIndices(handle, set) : 0;
    });
}

// Each returns how many uint32 values it left in the buffer, or -1 on error.

CDT_C_API int32_t cdt_find_encroached_fixed_edges(void* handle)
{
    int32_t n = -1;
    return guard(handle, [&] { n = keepEdges(handle, H(handle)->cdt.findEncroachedFixedEdges()); })
               ? -1
               : n;
}

CDT_C_API int32_t cdt_find_unrefined_triangles(
    void* handle,
    int32_t refinementCriterion,
    double refinementThreshold)
{
    int32_t n = -1;
    return guard(handle, [&] {
               n = keepIndices(
                   handle,
                   H(handle)->cdt.findUnrefinedTriangles(
                       static_cast<CDT::RefinementCriterion::Enum>(refinementCriterion),
                       refinementThreshold));
           })
               ? -1
               : n;
}

CDT_C_API int32_t cdt_collect_super_triangle(void* handle)
{
    int32_t n = -1;
    return guard(handle, [&] { n = keepIndices(handle, H(handle)->cdt.collectSuperTriangle()); })
               ? -1
               : n;
}

CDT_C_API int32_t cdt_collect_outer_triangles(void* handle)
{
    int32_t n = -1;
    return guard(handle, [&] { n = keepIndices(handle, H(handle)->cdt.collectOuterTriangles()); })
               ? -1
               : n;
}

CDT_C_API int32_t cdt_collect_outer_triangles_and_holes(void* handle)
{
    int32_t n = -1;
    return guard(handle, [&] {
               n = keepIndices(handle, H(handle)->cdt.collectOuterTrianglesAndHoles());
           })
               ? -1
               : n;
}

// Writes one depth for each triangle.
CDT_C_API int32_t cdt_calculate_triangle_depths(void* handle, uint16_t* depths)
{
    return guard(handle, [&] {
        const std::vector<CDT::LayerDepth> d = H(handle)->cdt.calculateTriangleDepths();
        std::copy(d.begin(), d.end(), depths);
    });
}

// Reading. Vertices and triangles are read in place: the pointers stay good until the
// triangulation next changes.

// Points to the vertices as x, y pairs.
CDT_C_API const double* cdt_vertices(void* handle, int32_t* count)
{
    const auto& vertices = H(handle)->cdt.vertices;
    *count = static_cast<int32_t>(vertices.size());
    return vertices.empty() ? nullptr : &vertices[0].x;
}

// Points to the triangles as three vertices, counter-clockwise, then three neighbours, the
// largest uint32 where there is none.
CDT_C_API const uint32_t* cdt_triangles(void* handle, int32_t* count)
{
    static_assert(sizeof(CDT::Triangle) == 6 * sizeof(uint32_t), "Triangle is six indices");
    const auto& triangles = H(handle)->cdt.triangles;
    *count = static_cast<int32_t>(triangles.size());
    return triangles.empty() ? nullptr : triangles[0].vertices.data();
}

CDT_C_API int32_t cdt_fixed_edges(void* handle)
{
    const CDT::EdgeUSet& edges = H(handle)->cdt.fixedEdges;
    return keepEdges(handle, CDT::EdgeVec(edges.begin(), edges.end()));
}

// Keeps each overlapping edge as v1, v2, count, and returns how many values that is.
CDT_C_API int32_t cdt_overlap_count(void* handle)
{
    std::vector<uint32_t>& buffer = H(handle)->buffer;
    buffer.clear();
    for(const auto& entry : H(handle)->cdt.overlapCount)
    {
        buffer.push_back(entry.first.v1());
        buffer.push_back(entry.first.v2());
        buffer.push_back(entry.second);
    }
    return static_cast<int32_t>(buffer.size());
}

// Keeps each piece of a split or overlapping constraint edge with one of its originals, as
// piece v1, v2, original v1, v2, and returns how many values that is.
CDT_C_API int32_t cdt_piece_to_originals(void* handle)
{
    std::vector<uint32_t>& buffer = H(handle)->buffer;
    buffer.clear();
    for(const auto& entry : H(handle)->cdt.pieceToOriginals)
        for(const CDT::Edge& original : entry.second)
        {
            buffer.push_back(entry.first.v1());
            buffer.push_back(entry.first.v2());
            buffer.push_back(original.v1());
            buffer.push_back(original.v2());
        }
    return static_cast<int32_t>(buffer.size());
}

// Checks

CDT_C_API int32_t cdt_verify_topology(void* handle)
{
    return CDT::verifyTopology(H(handle)->cdt) ? 1 : 0;
}

CDT_C_API int32_t cdt_verify_winding(void* handle)
{
    return CDT::verifyWinding(H(handle)->cdt) ? 1 : 0;
}

// Geometry. Points are x, y pairs.

CDT_C_API double cdt_area(const double* a, const double* b, const double* c)
{
    return CDT::area(v2d(a), v2d(b), v2d(c));
}

CDT_C_API void cdt_circumcenter(const double* a, const double* b, const double* c, double* result)
{
    const CDT::V2d<double> p = CDT::circumcenter(v2d(a), v2d(b), v2d(c));
    result[0] = p.x;
    result[1] = p.y;
}

CDT_C_API double cdt_smallest_angle(const double* a, const double* b, const double* c)
{
    return CDT::smallestAngle(v2d(a), v2d(b), v2d(c));
}

CDT_C_API double cdt_deg_to_rad(double degrees)
{
    return CDT::degToRad(degrees);
}
