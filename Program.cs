using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MazeGen
{
    class Program
    {
        static void Main(string[] args)
        {
            MazeMaker maker = new MazeMaker();
            Grid maze = maker.MakeMaze(int.Parse(args[0]), int.Parse(args[1]), int.Parse(args[2]));
            using (Stream stream = new FileStream("output.json", FileMode.Create)) {
                new MazePixelator().DrawMaze(maze, stream);
            }
        }
    }

    class MazeMaker
    {
        Grid grid;
        List<Coord> currentList = new List<Coord>();
        Random rand = new Random();
        Direction[] allDirections = { new Direction(-1, 0, 0), new Direction(1, 0, 0), new Direction(0, -1, 0), new Direction(0, 1, 0), new Direction(0, 0, -1), new Direction(0, 0, 1) };

        public Grid MakeMaze(int sizeX, int sizeY, int sizeZ)
        {
            grid = new Grid(sizeX, sizeY, sizeZ);
            Coord randomCoord = new Coord(rand.Next(sizeX), rand.Next(sizeY), rand.Next(sizeZ));
            currentList.Add(randomCoord);
            grid[randomCoord].Visited = true;
            while (currentList.Count > 0) {
                BuildPassage();
            }
            return grid;
        }

        private void BuildPassage()
        {
            int indexOfCoord;

            if (rand.Next(100) < 80) {
                indexOfCoord = currentList.Count - 1;  // Current algorithm; choose last.
            }
            else {
                indexOfCoord = rand.Next(currentList.Count);
            }

            Coord coord = currentList[indexOfCoord];

            bool[] isUnvisited = new bool[allDirections.Length];
            int countUnvisited = 0;
            for (int i = 0; i < allDirections.Length; ++i) {
                Coord adjacent = coord.Move(allDirections[i]);
                if (grid.Inside(adjacent) && !grid[adjacent].Visited) {
                    isUnvisited[i] = true;
                    countUnvisited += 1;
                }
            }

            if (countUnvisited == 0) {
                // No unvisited neighbors. Remove from the current list.
                currentList.RemoveAt(indexOfCoord);
            }
            else {
                int dirIndex = rand.Next(countUnvisited);
                Direction dirToTunnel = allDirections[0];
                for (int i = 0; i < allDirections.Length; ++i) {
                    if (isUnvisited[i]) {
                        if (dirIndex == 0) {
                            dirToTunnel = allDirections[i];
                            break;
                        }
                        else {
                            dirIndex -= 1;
                        }
                    }
                }

                Coord tunnelTo = coord.Move(dirToTunnel);
                grid[coord].SetPassage(dirToTunnel, true);
                grid[tunnelTo].SetPassage(dirToTunnel.Opposite, true);
                grid[tunnelTo].Visited = true;
                currentList.Add(tunnelTo);
            }
        }
    }

    class MazePrinter
    {
        Grid grid;
        char[,] pixels;

        public void PrintMaze(Grid grid)
        {
            this.grid = grid;
            FillAllPixels();

            for (int x = 0; x < grid.SizeX; ++x) {
                for (int y = 0; y < grid.SizeY; ++y) {
                    DrawCell(new Coord(x, y, 0));
                }
            }

            OutputPixels();
        }

        private void DrawCell(Coord coord)
        {
            int pixX = coord.X * 2 + 1, pixY = coord.Y * 2 + 1;
            pixels[pixX, pixY] = ' ';

            Cell cell = grid[coord];
            if (cell.GetPassage(new Direction(-1, 0, 0))) {
                pixels[pixX - 1, pixY] = ' ';
            }
            if (cell.GetPassage(new Direction(1, 0, 0))) {
                pixels[pixX + 1, pixY] = ' ';
            }
            if (cell.GetPassage(new Direction(0, -1, 0))) {
                pixels[pixX, pixY - 1] = ' ';
            }
            if (cell.GetPassage(new Direction(0, 1, 0))) {
                pixels[pixX, pixY + 1] = ' ';
            }
        }

        private void FillAllPixels()
        {
            pixels = new char[grid.SizeX * 2 + 1, grid.SizeY * 2 + 1];
            for (int x = 0; x < grid.SizeX * 2 + 1; ++x) {
                for (int y = 0; y < grid.SizeY * 2 + 1; ++y) {
                    pixels[x, y] = '#';
                }
            }
        }

        private void OutputPixels()
        {
            for (int y = 0; y < grid.SizeY * 2 + 1; ++y) {
                for (int x = 0; x < grid.SizeX * 2 + 1; ++x) {
                    Console.Write(pixels[x, y]);
                    Console.Write(pixels[x, y]);
                }
                Console.WriteLine();
            }
        }
    }

    class MazePixelator
    {
        Grid grid;
        byte[,,] pixels;

        const int HALFWIDTH = 2;
        const int CORRWIDTH = 7;
        const int MULTIPLIER = (HALFWIDTH * 2 + CORRWIDTH + 1);
        const int OFFSET = HALFWIDTH;
        const bool BOUNCERS = true;

        Direction posX = new Direction(1, 0, 0), negX = new Direction(-1, 0, 0);
        Direction posY = new Direction(0, 1, 0), negY = new Direction(0, -1, 0);
        Direction posZ = new Direction(0, 0, 1), negZ = new Direction(0, 0, -1);
        Direction[] allDirections = { new Direction(-1, 0, 0), new Direction(1, 0, 0), new Direction(0, -1, 0), new Direction(0, 1, 0), new Direction(0, 0, -1), new Direction(0, 0, 1) };

        public void DrawMaze(Grid grid, Stream stream)
        {
            this.grid = grid;
            FillAllPixels();

            for (int x = 0; x < grid.SizeX; ++x) {
                for (int y = 0; y < grid.SizeY; ++y) {
                    for (int z = 0; z < grid.SizeZ; ++z) {
                        DrawCell(new Coord(x, y, z));
                    }
                }
            }

            OutputDescription(stream);
        }

        private void FillAllPixels()
        {
            pixels = new byte[grid.SizeX * MULTIPLIER - CORRWIDTH, grid.SizeY * MULTIPLIER - CORRWIDTH + 1, grid.SizeZ * MULTIPLIER - CORRWIDTH];
        }

        private void SetPixel(Coord pixCoord, byte value)
        {
            if (pixCoord.X >= 0 && pixCoord.X < pixels.GetLength(0) && pixCoord.Y >= 0 && pixCoord.Y < pixels.GetLength(1) && pixCoord.Z >= 0 && pixCoord.Z < pixels.GetLength(2)) {
                pixels[pixCoord.X, pixCoord.Y, pixCoord.Z] = value;
            }
        }


        private void DrawCell(Coord mazeCoord)
        {
            Coord pixCoord = new Coord(mazeCoord.X * MULTIPLIER + OFFSET, mazeCoord.Y * MULTIPLIER + OFFSET + 1, mazeCoord.Z * MULTIPLIER + OFFSET);

            Cell cell = grid[mazeCoord];

            foreach (Direction d in allDirections) {
                if (cell.GetPassage(d)) {
                    DrawPassage(pixCoord, cell, d);
                }
                else {
                    if (BOUNCERS && d.Dy == -1 && cell.GetPassage(d.Opposite)) {
                        DrawBouncer(pixCoord, cell, d);
                    }
                    else {
                        DrawWall(pixCoord, cell, d);
                    }
                }
            }
        }

        private void DrawPassage(Coord pixCoord, Cell cell, Direction d)
        {
            Direction perp1, perp2;
            d.TwoPerpendicular(out perp1, out perp2);

            for (int l = HALFWIDTH; l <= HALFWIDTH + CORRWIDTH + 1; ++l) {
                for (int i = -HALFWIDTH; i <= HALFWIDTH; ++i) {
                    SetPixel(pixCoord.Move(d, l).Move(perp1, HALFWIDTH).Move(perp2, i), 1);
                    SetPixel(pixCoord.Move(d, l).Move(perp1.Opposite, HALFWIDTH).Move(perp2, i), 1);
                    SetPixel(pixCoord.Move(d, l).Move(perp2, HALFWIDTH).Move(perp1, i), 1);
                    SetPixel(pixCoord.Move(d, l).Move(perp2.Opposite, HALFWIDTH).Move(perp1, i), 1);
                }
            }
        }

        private void DrawWall(Coord pixCoord, Cell cell, Direction d)
        {
            Coord wallCenter = pixCoord.Move(d, HALFWIDTH);

            Direction perp1, perp2;
            d.TwoPerpendicular(out perp1, out perp2);

            for (int i = -HALFWIDTH; i <= HALFWIDTH; ++i) {
                for (int j = -HALFWIDTH; j <= HALFWIDTH; ++j) {
                    SetPixel(wallCenter.Move(perp1, i).Move(perp2, j), 1);
                }
            }
        }

        private void DrawBouncer(Coord pixCoord, Cell cell, Direction d)
        {
            Coord wallCenter = pixCoord.Move(d, HALFWIDTH);

            Direction perp1, perp2;
            d.TwoPerpendicular(out perp1, out perp2);

            for (int i = -(HALFWIDTH - 1); i <= (HALFWIDTH - 1); ++i) {
                for (int j = -(HALFWIDTH - 1); j <= (HALFWIDTH - 1); ++j) {
                    SetPixel(wallCenter.Move(perp1, i).Move(perp2, j), 2);
                    SetPixel(wallCenter.Move(d, 1).Move(perp1, i).Move(perp2, j), 1);
                }
            }
        }


        private void OutputPixels()
        {
            for (int z = 0; z < pixels.GetLength(2); ++z) {
                for (int y = 0; y < pixels.GetLength(1); ++y) {
                    for (int x = 0; x < pixels.GetLength(0); ++x) {
                        Console.Write(pixels[x, y, z] > 0 ? "##" : "  ");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine();
            }
        }

        private void OutputDescription(Stream stream)
        {
            Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { Indented = true, SkipValidation = false });

            writer.WriteStartObject();

            writer.WriteNumber("xSize", pixels.GetLength(0));
            writer.WriteNumber("ySize", pixels.GetLength(1));
            writer.WriteNumber("zSize", pixels.GetLength(2));

            writer.WriteStartArray("blocks");

            for (int x = 0; x < pixels.GetLength(0); ++x) {
                writer.WriteStartArray();

                for (int y = 0; y < pixels.GetLength(1); ++y) {
                    StringBuilder strBuilder = new StringBuilder();
                    for (int z = 0; z < pixels.GetLength(2); ++z) {
                        strBuilder.Append(pixels[x, y, z]);
                    }

                    writer.WriteStringValue(strBuilder.ToString());
                }

                writer.WriteEndArray();
            }

            writer.WriteEndArray();

            writer.WriteEndObject();

            writer.Dispose();
        }

    }

    class Grid
    {
        int sizeX, sizeY, sizeZ;
        Cell[,,] cells;

        public Grid(int sizeX, int sizeY, int sizeZ)
        {
            this.sizeX = sizeX;
            this.sizeY = sizeY;
            this.sizeZ = sizeZ;
            cells = new Cell[sizeX, sizeY, sizeZ];

            for (int x = 0; x < sizeX; ++x) {
                for (int y = 0; y < sizeY; ++y) {
                    for (int z = 0; z < sizeZ; ++z) {
                        cells[x, y, z] = new Cell();
                    }
                }
            }
        }

        public Cell this[Coord coord] {
            get {
                return cells[coord.X, coord.Y, coord.Z];
            }
        }

        public bool Inside(Coord coord)
        {
            return coord.X >= 0 && coord.X < sizeX && coord.Y >= 0 && coord.Y < sizeY && coord.Z >= 0 && coord.Z < sizeZ;
        }

        public int SizeX => sizeX;
        public int SizeY => sizeY;
        public int SizeZ => sizeZ;
    }

    class Cell
    {
        public bool Visited;
        bool[] walls = new bool[6];

        public void SetPassage(Direction d, bool passage)
        {
            walls[d.Index] = passage;
        }

        public bool GetPassage(Direction d)
        {
            return walls[d.Index];
        }

    }

    struct Coord
    {
        public int X, Y, Z;
        public Coord(int x, int y, int z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public Coord Move(Direction d)
        {
            return new Coord(X + d.Dx, Y + d.Dy, Z + d.Dz);
        }

        public Coord Move(Direction d, int dist)
        {
            return new Coord(X + d.Dx * dist, Y + d.Dy * dist, Z + d.Dz * dist);
        }
    }

    struct Direction
    {
        public Direction(int dx, int dy, int dz)
        {
            this.Dx = (sbyte)dx;
            this.Dy = (sbyte)dy;
            this.Dz = (sbyte)dz;
        }

        public sbyte Dx, Dy, Dz;
        public int Index {
            get {
                int i = Dx + (2 * Dy) + (3 * Dz);
                if (i < 0)
                    i++;
                return i + 2;
            }
        }

        public Direction Opposite => new Direction(-Dx, -Dy, -Dz);

        public void TwoPerpendicular(out Direction d1, out Direction d2)
        {
            if (Dx != 0) {
                d1 = new Direction(0, 1, 0);
                d2 = new Direction(0, 0, 1);
            }
            else if (Dy != 0) {
                d1 = new Direction(1, 0, 0);
                d2 = new Direction(0, 0, 1);
            }
            else {
                d1 = new Direction(1, 0, 0);
                d2 = new Direction(0, 1, 0);
            }
        }

        static (int, int)[] tuples = { (-1, 0), (1, 0), (0, -1), (0, 1) };

        // All 4 perpendicular directions
        public Direction[] Perps()
        {
            Direction[] result = new Direction[4];
            int index = 0;

            foreach (var tuple in tuples) {
                if (Dx != 0)
                    result[index++] = new Direction(0, tuple.Item1, tuple.Item2);
                else if (Dy != 0)
                    result[index++] = new Direction(tuple.Item1, 0, tuple.Item2);
                else
                    result[index++] = new Direction(tuple.Item1, tuple.Item2, 0);
            }

            return result;
        }

        // 2 direction perpendicular to two directions
        public Direction[] Perps(Direction d2)
        {
            Direction[] result = new Direction[2];
            int index = 0;
            for (int i = -1; i <= 1; i += 2) {
                if (Dx == 0 && d2.Dx == 0)
                    result[index++] = new Direction(i, 0, 0);
                else if (Dy == 0 && d2.Dy == 0)
                    result[index++] = new Direction(0, i, 0);
                else
                    result[index++] = new Direction(0, 0, i);
            }
            return result;
        }
    }
}
