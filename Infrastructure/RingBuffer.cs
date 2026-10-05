namespace GpuMonitor.Infrastructure;
// Owned exclusively by the UI thread; capacity never grows.
public sealed class RingBuffer<T>(int capacity)
{
 private readonly T[] items = new T[capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))];
 private int next;
 public int Count { get; private set; }
 public void Clear() { Array.Clear(items); next = 0; Count = 0; }
 public int Capacity => items.Length;
 public void Add(T item) { items[next] = item; next = (next + 1) % Capacity; Count = Math.Min(Count + 1, Capacity); }
 public T this[int index] => index >= 0 && index < Count ? items[(next - Count + index + Capacity) % Capacity] : throw new ArgumentOutOfRangeException(nameof(index));
}

