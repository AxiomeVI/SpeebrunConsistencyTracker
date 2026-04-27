using System;
using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Domain.Attempts;

public sealed class AttemptMatrix
{
    private RoomCell[] _cells;
    private int _columnCapacity;
    private int _rowCapacity;

    public int ColumnCount { get; private set; }
    public int RowCount { get; private set; }

    public AttemptMatrix(int initialColumnCapacity = 16, int initialRowCapacity = 64)
    {
        _columnCapacity = initialColumnCapacity;
        _rowCapacity = initialRowCapacity;
        _cells = new RoomCell[_rowCapacity * _columnCapacity];
    }

    public RoomCell this[int attempt, int room]
    {
        get => room < ColumnCount ? _cells[attempt * _columnCapacity + room] : RoomCell.NotReached;
    }

    public void SetCell(int attempt, int room, RoomCell value)
    {
        _cells[attempt * _columnCapacity + room] = value;
    }

    public ReadOnlySpan<RoomCell> GetRow(int attempt)
        => _cells.AsSpan(attempt * _columnCapacity, ColumnCount);

    public ColumnEnumerator GetColumn(int room) => new(_cells, room, _columnCapacity, RowCount);

    public void AddRow()
    {
        if (RowCount >= _rowCapacity)
        {
            _rowCapacity = Math.Max(_rowCapacity * 2, 1);
            var newCells = new RoomCell[_rowCapacity * _columnCapacity];
            Array.Copy(_cells, newCells, RowCount * _columnCapacity);
            _cells = newCells;
        }
        RowCount++;
    }

    public void EnsureColumns(int requiredCount)
    {
        if (requiredCount <= ColumnCount) return;

        if (requiredCount > _columnCapacity)
        {
            int newCapacity = Math.Max(_columnCapacity * 2, requiredCount);
            var newCells = new RoomCell[_rowCapacity * newCapacity];
            for (int r = 0; r < RowCount; r++)
                Array.Copy(_cells, r * _columnCapacity, newCells, r * newCapacity, ColumnCount);
            _cells = newCells;
            _columnCapacity = newCapacity;
        }

        ColumnCount = requiredCount;
    }

    public struct ColumnEnumerator : IEnumerable<RoomCell>, IEnumerator<RoomCell>
    {
        private readonly RoomCell[] _cells;
        private readonly int _room;
        private readonly int _stride;
        private readonly int _rowCount;
        private int _currentRow;

        public ColumnEnumerator(RoomCell[] cells, int room, int stride, int rowCount)
        {
            _cells = cells;
            _room = room;
            _stride = stride;
            _rowCount = rowCount;
            _currentRow = -1;
        }

        public RoomCell Current => _cells[_currentRow * _stride + _room];
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            _currentRow++;
            return _currentRow < _rowCount;
        }

        public void Reset() => _currentRow = -1;
        public void Dispose() { }

        public ColumnEnumerator GetEnumerator() => this;
        IEnumerator<RoomCell> IEnumerable<RoomCell>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
