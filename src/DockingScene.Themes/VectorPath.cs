using System.Collections.Immutable;
using System.Globalization;

namespace DockingScene.Themes;

public enum VectorSegmentKind { Move, Line, Quadratic, Cubic, Arc, Close }
public readonly record struct VectorSegment(VectorSegmentKind Kind, double X, double Y,
    double Control1X = 0, double Control1Y = 0, double Control2X = 0, double Control2Y = 0,
    double RadiusX = 0, double RadiusY = 0, double Rotation = 0, bool LargeArc = false, bool Sweep = false);

/// <summary>UI-independent SVG path parser. Supports absolute/relative M/L/H/V/Q/T/C/S/A/Z,
/// repeated operands, smooth control reflection and exponent notation. Limits and finite checks bound untrusted input.</summary>
public static class VectorPath
{
    public static ImmutableArray<VectorSegment> Parse(string data, int maximumSegments = 10000)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (maximumSegments < 1) throw new ArgumentOutOfRangeException(nameof(maximumSegments));
        if (data.Length > 1_000_000) throw new FormatException("Vector path is too long.");
        return new Parser(data, maximumSegments).Parse();
    }
    private sealed class Parser(string data, int maximumSegments)
    {
        private int _position;
        private bool _afterNumber, _hasMove, _cubic, _quadratic;
        private double _x, _y, _startX, _startY, _controlX, _controlY;
        private readonly ImmutableArray<VectorSegment>.Builder _segments = ImmutableArray.CreateBuilder<VectorSegment>();
        public ImmutableArray<VectorSegment> Parse()
        {
            char command = '\0';
            while (true)
            {
                WhiteSpace(); if (_position == data.Length) break;
                if (char.IsAsciiLetter(data[_position])) { command = data[_position++]; _afterNumber = false; }
                if (command == '\0') throw Error("Expected a path command.");
                bool relative = char.IsAsciiLetterLower(command);
                char op = char.ToUpperInvariant(command);
                if (!_hasMove && op != 'M') throw Error("A path must begin with a move.");
                double x = _x, y = _y, c1x = 0, c1y = 0, c2x = 0, c2y = 0;
                switch (op)
                {
                    case 'M':
                        (x, y) = Point(relative); _startX = x; _startY = y; _hasMove = true;
                        Add(new(VectorSegmentKind.Move, x, y)); command = relative ? 'l' : 'L'; break;
                    case 'L': (x, y) = Point(relative); Add(new(VectorSegmentKind.Line, x, y)); break;
                    case 'H': x = Number() + (relative ? _x : 0); Add(new(VectorSegmentKind.Line, x, y)); break;
                    case 'V': y = Number() + (relative ? _y : 0); Add(new(VectorSegmentKind.Line, x, y)); break;
                    case 'Q':
                        (c1x, c1y) = Point(relative); (x, y) = Point(relative);
                        Add(new(VectorSegmentKind.Quadratic, x, y, c1x, c1y)); break;
                    case 'T':
                        c1x = _quadratic ? 2 * _x - _controlX : _x; c1y = _quadratic ? 2 * _y - _controlY : _y;
                        (x, y) = Point(relative); Add(new(VectorSegmentKind.Quadratic, x, y, c1x, c1y)); break;
                    case 'C':
                        (c1x, c1y) = Point(relative); (c2x, c2y) = Point(relative); (x, y) = Point(relative);
                        Add(new(VectorSegmentKind.Cubic, x, y, c1x, c1y, c2x, c2y)); break;
                    case 'S':
                        c1x = _cubic ? 2 * _x - _controlX : _x; c1y = _cubic ? 2 * _y - _controlY : _y;
                        (c2x, c2y) = Point(relative); (x, y) = Point(relative);
                        Add(new(VectorSegmentKind.Cubic, x, y, c1x, c1y, c2x, c2y)); break;
                    case 'A':
                        double rx = Number(), ry = Number(), rotation = Number(); bool large = Flag(), sweep = Flag();
                        if (rx < 0 || ry < 0) throw Error("Arc radii cannot be negative.");
                        (x, y) = Point(relative);
                        Add(rx == 0 || ry == 0 ? new(VectorSegmentKind.Line, x, y) : new(VectorSegmentKind.Arc, x, y, RadiusX: rx, RadiusY: ry, Rotation: rotation, LargeArc: large, Sweep: sweep));
                        break;
                    case 'Z': x = _startX; y = _startY; Add(new(VectorSegmentKind.Close, x, y)); command = '\0'; break;
                    default: throw Error("Unknown path command: " + command);
                }
                _x = x; _y = y; _cubic = op is 'C' or 'S'; _quadratic = op is 'Q' or 'T';
                _controlX = _cubic ? c2x : c1x; _controlY = _cubic ? c2y : c1y;
            }
            return _segments.ToImmutable();
        }
        private (double X, double Y) Point(bool relative) => (Number() + (relative ? _x : 0), Number() + (relative ? _y : 0));
        private void Add(VectorSegment segment)
        {
            if (_segments.Count >= maximumSegments) throw Error("Vector segment limit exceeded.");
            if (!double.IsFinite(segment.X) || !double.IsFinite(segment.Y) || !double.IsFinite(segment.Control1X) || !double.IsFinite(segment.Control1Y) ||
                !double.IsFinite(segment.Control2X) || !double.IsFinite(segment.Control2Y)) throw Error("Coordinates must be finite.");
            _segments.Add(segment);
        }
        private void WhiteSpace() { while (_position < data.Length && data[_position] is ' ' or '\t' or '\r' or '\n' or '\f') _position++; }
        private void Separator()
        {
            WhiteSpace();
            if (_position < data.Length && data[_position] == ',')
            {
                if (!_afterNumber) throw Error("Unexpected comma.");
                _position++; WhiteSpace();
            }
        }
        private bool Flag()
        {
            Separator();
            if (_position >= data.Length || data[_position] is not ('0' or '1')) throw Error("An arc flag must be 0 or 1.");
            _afterNumber = true; return data[_position++] == '1';
        }
        private double Number()
        {
            Separator(); int start = _position, digits = 0;
            if (_position < data.Length && data[_position] is '+' or '-') _position++;
            while (_position < data.Length && char.IsAsciiDigit(data[_position])) { _position++; digits++; }
            if (_position < data.Length && data[_position] == '.')
            {
                _position++;
                while (_position < data.Length && char.IsAsciiDigit(data[_position])) { _position++; digits++; }
            }
            if (digits == 0) throw Error("Expected a number.");
            if (_position < data.Length && data[_position] is 'e' or 'E')
            {
                _position++; if (_position < data.Length && data[_position] is '+' or '-') _position++;
                int exponent = _position;
                while (_position < data.Length && char.IsAsciiDigit(data[_position])) _position++;
                if (exponent == _position) throw Error("Incomplete exponent.");
            }
            if (!double.TryParse(data.AsSpan(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                throw Error("Invalid or non-finite number.");
            _afterNumber = true; return value;
        }
        private FormatException Error(string message) => new(message + " Offset " + _position.ToString(CultureInfo.InvariantCulture) + ".");
    }
}
