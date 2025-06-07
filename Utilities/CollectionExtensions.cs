using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XB2Midi.Utilities
{
    /// <summary>
    /// Extension methods for collection classes
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Adds a range of items to an ObservableCollection
        /// </summary>
        /// <typeparam name="T">Type of items in the collection</typeparam>
        /// <param name="collection">The collection to add to</param>
        /// <param name="items">The items to add</param>
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
                
            if (items == null)
                throw new ArgumentNullException(nameof(items));
                
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }
    }
}