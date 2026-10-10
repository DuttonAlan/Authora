// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Paging;

/// <summary>
///     Represents the requested page of a list.
/// </summary>
/// <param name="Page">The 1-based number of the page.</param>
/// <param name="Size">The number of items per page.</param>
public sealed record PageRequest(int Page, int Size)
{
    /// <summary>
    ///     Number of items to skip to reach the requested page.
    /// </summary>
    public int Skip => (Page - 1) * Size;
}
