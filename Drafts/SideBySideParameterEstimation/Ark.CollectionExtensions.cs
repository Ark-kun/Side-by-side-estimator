using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Ark.Collections {
    public static class DictionaryExtensions {
        public static TValue GetOrCreateValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : class, new() {
            Contract.Requires(dictionary != null);

            TValue value;
            if (!dictionary.TryGetValue(key, out value)) {
                value = new TValue();
                dictionary.Add(key, value);
            }
            return value;
        }

        public static TValue GetOrCreateValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> factory) {
            Contract.Requires(dictionary != null);

            TValue value;
            if (!dictionary.TryGetValue(key, out value)) {
                value = factory();
                dictionary.Add(key, value);
            }
            return value;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue)) {
            Contract.Requires(dictionary != null);

            TValue value;
            if (dictionary.TryGetValue(key, out value)) {
                return value;
            } else {
                return defaultValue;
            }
        }

        public static TValue TransformValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue, TValue> transformation, TValue defaultValue = default(TValue)) {
            Contract.Requires(dictionary != null);

            TValue value;
            if (!dictionary.TryGetValue(key, out value)) {
                value = defaultValue;
            }
            value = transformation(value);
            dictionary[key] = value;

            return value;
        }

        public static TValue TransformValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue, TValue> transformation, Func<TValue> missingValueFactory) {
            Contract.Requires(dictionary != null);

            TValue value;
            if (!dictionary.TryGetValue(key, out value)) {
                value = missingValueFactory();
            }
            value = transformation(value);
            dictionary[key] = value;

            return value;
        }

        public static Dictionary<T, int> ToIndexDictionary<T>(this IEnumerable<T> source) {
            if (source == null) {
                throw new ArgumentNullException("source");
            }
            int index = 0;
            var dict = new Dictionary<T, int>();
            foreach (var item in source) {
                dict.Add(item, index++);
            }
            return dict;
        }

        public static int GetOrAddToIndexDictionary<TKey>(this IDictionary<TKey, int> dictionary, TKey key) {
            Contract.Requires(dictionary != null);

            int value;
            if (!dictionary.TryGetValue(key, out value)) {
                value = dictionary.Count;
                dictionary.Add(key, value);
            }
            return value;
        }

        public static int IncreaseNumberForKeyInMultiset<TKey>(this IDictionary<TKey, int> multiSet, TKey key) {
            Contract.Requires(multiSet != null);

            int value;
            if (!multiSet.TryGetValue(key, out value)) {
                value = 0;
            }
            ++value;
            multiSet[key] = value;

            return value;
        }
    }

    public static class CollectionExtensions {
        public static IList<T> SetAllValues<T>(this IList<T> list, T defaultValue = default(T)) {
            Contract.Requires(list != null);

            for (int i = 0; i < list.Count; ++i) {
                list[i] = defaultValue;
            }
            return list;
        }

        public static IList<T> SetAllValues<T>(this IList<T> list, Func<T> valueFactory) {
            Contract.Requires(list != null);
            Contract.Requires(valueFactory != null);

            for (int i = 0; i < list.Count; ++i) {
                list[i] = valueFactory();
            }
            return list;
        }

        public static IList<T> SetAllValues<T>(this IList<T> list, Func<int, T> valueFactory) {
            Contract.Requires(list != null);
            Contract.Requires(valueFactory != null);

            for (int i = 0; i < list.Count; ++i) {
                list[i] = valueFactory(i);
            }
            return list;
        }

        public static IList<T> ConstructAllValues<T>(this IList<T> list) where T : new() {
            Contract.Requires(list != null);

            for (int i = 0; i < list.Count; ++i) {
                list[i] = new T();
            }
            return list;
        }

        //public static TList ConstructAllValues<TList, T>(this TList list) where TList : IList<T> where T : new() {
        //    Contract.Requires(list != null);

        //    for (int i = 0; i < list.Count; ++i) {
        //        list[i] = new T();
        //    }
        //    return list;
        //}

        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items) {
            if (collection == null) {
                throw new ArgumentNullException("collection");
            }
            if (items == null) {
                throw new ArgumentNullException("items");
            }
            foreach (var item in items) {
                collection.Add(item);
            }
        }

        public static int RemoveWhere<T>(this ICollection<T> collection, Func<T, bool> predicate) {
            Contract.Requires(collection != null);
            Contract.Requires(predicate != null);

            var hashSet = collection as HashSet<T>;
            if (hashSet != null) {
                return hashSet.RemoveWhere(predicate);
            }
            var sortedSet = collection as SortedSet<T>;
            if (sortedSet != null) {
                return sortedSet.RemoveWhere(predicate);
            }
            var list = collection as IList<T>;
            if (list != null) {
                int removedCount = 0;
                for (int i = 0; i < list.Count; i++) {
                    if (predicate(list[i])) {
                        removedCount++;
                    } else if (removedCount > 0) {
                        list[i - removedCount] = list[i];
                    }
                }
                for (int i = 0; i < removedCount; i++) {
                    list.RemoveAt(list.Count - 1);
                }
                return removedCount;
            } else {
                var itemsToRemove = collection.Where(predicate).ToList();
                foreach (var item in itemsToRemove) {
                    collection.Remove(item);
                }
                return itemsToRemove.Count;
            }
        }

        public static List<T> RemoveAndReturn<T>(this ICollection<T> collection, Func<T, bool> predicate) {
            Contract.Requires(collection != null);
            Contract.Requires(predicate != null);

            var list = collection as IList<T>;
            if (list != null) {
                var removedItems = new List<T>();
                for (int i = 0; i < list.Count; i++) {
                    if (predicate(list[i])) {
                        removedItems.Add(list[i]);
                    } else if (removedItems.Count > 0) {
                        list[i - removedItems.Count] = list[i];
                    }
                }
                for (int i = 0; i < removedItems.Count; i++) {
                    list.RemoveAt(list.Count - 1);
                }
                return removedItems;
            } else {
                var itemsToRemove = collection.Where(predicate).ToList();

                var hashSet = collection as HashSet<T>;
                if (hashSet != null) {
                    hashSet.RemoveWhere(predicate);
                    return itemsToRemove;
                }

                var sortedSet = collection as SortedSet<T>;
                if (sortedSet != null) {
                    sortedSet.RemoveWhere(predicate);
                    return itemsToRemove;
                }

                foreach (var item in itemsToRemove) {
                    collection.Remove(item);
                }
                return itemsToRemove;
            }
        }

        public static void ReversedForEach<TSource>(this IList<TSource> list, Action<TSource> action) {
            if (list == null) {
                throw new ArgumentNullException("source");
            }
            if (action == null) {
                throw new ArgumentNullException("action");
            }
            for (int i = list.Count - 1; i >= 0; --i) {
                action(list[i]);
            }
        }

        public static void Modify<T>(this IList<T> items, Func<T, T> transform) {
            for (int i = 0; i < items.Count; i++) {
                items[i] = transform(items[i]);
            }
        }

        public static void Modify<T>(this IList<T> items, Func<int, T, T> transform) {
            for (int i = 0; i < items.Count; i++) {
                items[i] = transform(i, items[i]);
            }
        }
    }
}
