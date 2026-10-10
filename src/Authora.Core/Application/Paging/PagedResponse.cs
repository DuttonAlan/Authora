// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Paging;

/// <summary>
///     Represents one page of a list.
/// </summary>
/// <typeparam name="T">The type of the items.</typeparam>
/// <param name="Items">The items on the requested page.</param>
/// <param name="Total" example="42">The total number of items across all pages.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total);
