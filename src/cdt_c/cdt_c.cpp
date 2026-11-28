// cdt_c: a C interface over CDT's Triangulation<double>, for CSharpCDT. It covers what
// PythonCDT binds, one function per member, named as PythonCDT names them.
//
// Each call that can fail returns 0 on success, or 1 with the message kept for
// cdt_last_error. Indices are CDT's 32-bit VertInd and TriInd. A set or list CDT returns is
// kept in the handle: the call returns how many uint32 values it holds, and cdt_copy_buffer
// copies them out.
//
// Nothing is copied that CDT does not make us copy. Arrays coming in are read where they are,
// through CDT's iterator overloads. A vector CDT returns is kept as it is, moved into the
// handle; only sets and maps, which are not contiguous, are copied into a list first. Where CDT
// takes a set or a vector, the caller's array is copied into one: see toSet and toTriangles.
//
// Not bound: setCallbackHandler, which would need callbacks across the native boundary, the
// constructor taking a near-point locator, which is a template parameter, and
// VertTrisInternal, which CDT marks internal.

#include <CDT.h>
#include <extras/VerifyTopology.h>

#include <algorithm>
#include <cstdint>
#include <cstring>
#include <exception>
#include <string>
#include <utility>
#include <vector>

#if defined(_WIN32)
#define CDT_C_API extern "C" __declspec(dllexport)
#else
#define CDT_C_API extern "C" __attribute__((visibility("default")))
#endif

namespace
{
typedef CDT::Triangulation<double> Triangulation;

static_assert(sizeof(CDT::VertInd) == sizeof(uint32_t), "32-bit vertex indices");
static_assert(sizeof(CDT::TriInd) == sizeof(uint32_t), "32-bit triangle indices");
static_assert(sizeof(CDT::Edge) == 2 * sizeof(uint32_t), "Edge is two indices");

// A vertex and an edge as the caller lays them out, read in place.
struct XY
{
    double x, y;
};

struct Pair
{
    uint32_t v1, v2;
};

struct Handle
{
    Triangulation cdt;
    std::string error;
    // What the last call kept: indices in buffer, or edges in edges.
    std::vector<uint32_t> buffer;
    CDT::EdgeVec edges;
    bool keptEdges = false;

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

uint32_t pairStart(const Pair& e)
{
    return e.v1;
}

uint32_t pairEnd(const Pair& e)
{
    return e.v2;
}

double xOf(const XY& v)
{
    return v.x;
}

double yOf(const XY& v)
{
    return v.y;
}

// CDT takes these as a set, so the indices are copied into one.
CDT::TriIndUSet toSet(const uint32_t* triangles, int32_t count)
{
    return CDT::TriIndUSet(triangles, triangles + count);
}

// A set is not contiguous, so it is copied into the buffer.
int32_t keepIndices(void* handle, const CDT::TriIndUSet& indices)
{
    Handle* h = H(handle);
    h->buffer.assign(indices.begin(), indices.end());
    h->keptEdges = false;
    return static_cast<int32_t>(h->buffer.size());
}

// A vector is kept as it is.
int32_t keepIndices(void* handle, CDT::TriIndVec&& indices)
{
    Handle* h = H(handle);
    h->buffer = std::move(indices);
    h->keptEdges = false;
    return static_cast<int32_t>(h->buffer.size());
}

int32_t keepEdges(void* handle, CDT::EdgeVec&& edges)
{
    Handle* h = H(handle);
    h->edges = std::move(edges);
    h->keptEdges = true;
    return static_cast<int32_t>(2 * h->edges.size());
}

// Starts a list in the buffer, for what CDT keeps in sets and maps.
std::vector<uint32_t>& freshBuffer(void* handle)
{
    Handle* h = H(handle);
    h->buffer.clear();
    h->keptEdges = false;
    return h->buffer;
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
    const Handle* h = H(handle);
    if(h->keptEdges)
        std::memcpy(values, h->edges.data(), h->edges.size() * sizeof(CDT::Edge));
    else
        std::copy(h->buffer.begin(), h->buffer.end(), values);
}

// Building

// xy holds count points as x, y pairs.
CDT_C_API int32_t cdt_insert_vertices(void* handle, const double* xy, int32_t count)
{
    const XY* first = reinterpret_cast<const XY*>(xy);
    return guard(handle, [&] { H(handle)->cdt.insertVertices(first, first + count, xOf, yOf); });
}

// vertexPairs holds count edges as pairs of vertex indices.
CDT_C_API int32_t cdt_insert_edges(void* handle, const uint32_t* vertexPairs, int32_t count)
{
    const Pair* first = reinterpret_cast<const Pair*>(vertexPairs);
    return guard(handle, [&] {
        H(handle)->cdt.insertEdges(first, first + count, pairStart, pairEnd);
    });
}

CDT_C_API int32_t cdt_conform_to_edges(void* handle, const uint32_t* vertexPairs, int32_t count)
{
    const Pair* first = reinterpret_cast<const Pair*>(vertexPairs);
    return guard(handle, [&] {
        H(handle)->cdt.conformToEdges(first, first + count, pairStart, pairEnd);
    });
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

// Flips the edge between two triangles. For changing the triangulation by hand, as CDT warns.
CDT_C_API int32_t cdt_flip_edge(void* handle, uint32_t triangle, uint32_t opposite)
{
    return guard(handle, [&] { H(handle)->cdt.flipEdge(triangle, opposite); });
}

// Flips the edge between two triangles, given their vertices and neighbours as CDT's second
// flipEdge takes them.
CDT_C_API int32_t cdt_flip_edge_given(
    void* handle,
    uint32_t triangle,
    uint32_t opposite,
    uint32_t v1,
    uint32_t v2,
    uint32_t v3,
    uint32_t v4,
    uint32_t n1,
    uint32_t n2,
    uint32_t n3,
    uint32_t n4)
{
    return guard(handle, [&] {
        H(handle)->cdt.flipEdge(triangle, opposite, v1, v2, v3, v4, n1, n2, n3, n4);
    });
}

// Refining

// CDT's Unrefined, with fixed-size counts, laid out as CSharpCDT's Unrefined.
struct cdt_unrefined
{
    uint64_t shortEdgeTriangles, circumcenterOutside, circumcenterOnVertex, sharpFixedCorner,
        shortEdges, splitVertexInvalid;
};

// Refines until no triangle is bad by the criterion or maxVerticesToInsert are added. toErase,
// when count is not negative, holds triangles left as they are; the triangles that replace them
// are kept in the buffer, with their count in toEraseCount.
CDT_C_API int32_t cdt_refine_triangles(
    void* handle,
    uint32_t maxVerticesToInsert,
    int32_t refinementCriterion,
    double refinementThreshold,
    const uint32_t* toErase,
    int32_t count,
    double minEdgeLength,
    int32_t refinementOrder,
    cdt_unrefined* unrefined,
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
        *unrefined = cdt_unrefined{
            u.shortEdgeTriangles,
            u.circumcenterOutside,
            u.circumcenterOnVertex,
            u.sharpFixedCorner,
            u.shortEdges,
            u.splitVertexInvalid};
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
    std::vector<uint32_t>& buffer = freshBuffer(handle);
    for(const CDT::Edge& e : H(handle)->cdt.fixedEdges)
    {
        buffer.push_back(e.v1());
        buffer.push_back(e.v2());
    }
    return static_cast<int32_t>(buffer.size());
}

// Keeps each overlapping edge as v1, v2, count, and returns how many values that is.
CDT_C_API int32_t cdt_overlap_count(void* handle)
{
    std::vector<uint32_t>& buffer = freshBuffer(handle);
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
    std::vector<uint32_t>& buffer = freshBuffer(handle);
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

// Mappings between the triangulation's edges and the constraint edges given, kept in the buffer
// as, for each original edge, its v1, v2 and count, then that many values. Pieces are vertex
// pairs, two values each, and split vertices one each, from the edge's v1 to its v2.

CDT_C_API int32_t cdt_edge_to_pieces(void* handle)
{
    std::vector<uint32_t>& buffer = freshBuffer(handle);
    for(const auto& entry : CDT::EdgeToPiecesMapping(H(handle)->cdt.pieceToOriginals))
    {
        buffer.push_back(entry.first.v1());
        buffer.push_back(entry.first.v2());
        buffer.push_back(static_cast<uint32_t>(entry.second.size()));
        for(const CDT::Edge& piece : entry.second)
        {
            buffer.push_back(piece.v1());
            buffer.push_back(piece.v2());
        }
    }
    return static_cast<int32_t>(buffer.size());
}

CDT_C_API int32_t cdt_edge_to_split_vertices(void* handle)
{
    const Triangulation& cdt = H(handle)->cdt;
    std::vector<uint32_t>& buffer = freshBuffer(handle);
    for(const auto& entry : CDT::EdgeToSplitVertices(
            CDT::EdgeToPiecesMapping(cdt.pieceToOriginals), cdt.vertices))
    {
        buffer.push_back(entry.first.v1());
        buffer.push_back(entry.first.v2());
        buffer.push_back(static_cast<uint32_t>(entry.second.size()));
        buffer.insert(buffer.end(), entry.second.begin(), entry.second.end());
    }
    return static_cast<int32_t>(buffer.size());
}

// Free functions, on the caller's own arrays. Each returns 0, or 1 with the reason in error.

namespace
{
template <typename F>
int32_t guardFree(char* error, int32_t errorLength, F f)
{
    try
    {
        f();
        return 0;
    }
    catch(const std::exception& e)
    {
        if(error && errorLength > 0)
        {
            std::strncpy(error, e.what(), static_cast<size_t>(errorLength - 1));
            error[errorLength - 1] = '\0';
        }
    }
    return 1;
}

void writeInfo(const CDT::DuplicatesInfo& info, uint32_t* mapping, uint32_t* duplicates, int32_t* duplicateCount)
{
    // CDT gives size_t. The indices are below count, an int32, so they fit in 32 bits.
    const auto narrow = [](std::size_t i) { return static_cast<uint32_t>(i); };
    std::transform(info.mapping.begin(), info.mapping.end(), mapping, narrow);
    std::transform(info.duplicates.begin(), info.duplicates.end(), duplicates, narrow);
    *duplicateCount = static_cast<int32_t>(info.duplicates.size());
}
} // namespace

// Finds vertices that repeat an earlier one, reading xy where it is. mapping gets each vertex's
// index among those kept, and duplicates the indices of those that repeat, both with room for
// count. CDT returns both as new vectors of size_t, so they are copied out once.
//
// RemoveDuplicates, RemapEdges and RemoveDuplicatesAndRemapEdges are not here: CDT takes
// std::vectors for them, so binding them would copy the caller's arrays in and out. They are
// done in CSharpCDT on the caller's own memory, as CDT does them, from what this finds.
CDT_C_API int32_t cdt_find_duplicates(
    const double* xy,
    int32_t count,
    uint32_t* mapping,
    uint32_t* duplicates,
    int32_t* duplicateCount,
    char* error,
    int32_t errorLength)
{
    const XY* first = reinterpret_cast<const XY*>(xy);
    return guardFree(error, errorLength, [&] {
        writeInfo(
            CDT::FindDuplicates<double>(first, first + count, xOf, yOf),
            mapping,
            duplicates,
            duplicateCount);
    });
}

namespace
{
// CDT takes the triangles as a TriangleVec, so they are copied into one.
CDT::TriangleVec toTriangles(const uint32_t* triangles, int32_t count)
{
    const CDT::Triangle* first = reinterpret_cast<const CDT::Triangle*>(triangles);
    return CDT::TriangleVec(first, first + count);
}
} // namespace

// Writes every edge of the triangles once, as vertex pairs, into room for 3 * count edges, and
// returns how many there are.
CDT_C_API int32_t cdt_extract_edges_from_triangles(
    const uint32_t* triangles,
    int32_t count,
    uint32_t* vertexPairs)
{
    int32_t n = 0;
    for(const CDT::Edge& e : CDT::extractEdgesFromTriangles(toTriangles(triangles, count)))
    {
        vertexPairs[2 * n] = e.v1();
        vertexPairs[2 * n + 1] = e.v2();
        ++n;
    }
    return n;
}

// Writes the triangles touching each vertex: offsets has room for vertexCount + 1, and vertex i's
// triangles are triangleIndices[offsets[i]] up to triangleIndices[offsets[i + 1]], with room for
// 3 * count.
CDT_C_API void cdt_calculate_triangles_by_vertex(
    const uint32_t* triangles,
    int32_t count,
    int32_t vertexCount,
    int32_t* offsets,
    uint32_t* triangleIndices)
{
    const CDT::VerticesTriangles byVertex =
        CDT::calculateTrianglesByVertex(toTriangles(triangles, count), vertexCount);
    int32_t n = 0;
    for(int32_t i = 0; i < vertexCount; ++i)
    {
        offsets[i] = n;
        for(const CDT::TriInd t : byVertex[i])
            triangleIndices[n++] = t;
    }
    offsets[vertexCount] = n;
}
