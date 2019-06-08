﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace SoulsFormats
{
    /// <summary>
    /// A file that defines the placement and properties of navmeshes in BB, DS3, and Sekiro. Extension: .nva
    /// </summary>
    public class NVA : SoulsFile<NVA>
    {
        /// <summary>
        /// Version of the format, except it's useless because the sections have individual versions.
        /// </summary>
        public enum NVAVersion : uint
        {
            /// <summary>
            /// Used for a single BB test map, m29_03_10_00; has no Section8
            /// </summary>
            OldBloodborne = 3,

            /// <summary>
            /// Dark Souls 3 and Bloodborne
            /// </summary>
            DarkSouls3 = 4,

            /// <summary>
            /// Sekiro
            /// </summary>
            Sekiro = 5,
        }

        /// <summary>
        /// The format version of this file.
        /// </summary>
        public NVAVersion Version { get; set; }

        /// <summary>
        /// Navmesh instances in the map.
        /// </summary>
        public NavmeshSection Navmeshes { get; set; }

        /// <summary>
        /// Unknown.
        /// </summary>
        public Section1 Entries1 { get; set; }

        /// <summary>
        /// Unknown.
        /// </summary>
        public Section2 Entries2 { get; set; }

        /// <summary>
        /// Connections between different navmeshes.
        /// </summary>
        public ConnectorSection Connectors { get; set; }

        /// <summary>
        /// Points used to connect different navmeshes.
        /// </summary>
        public ConnectorPointSection ConnectorPoints { get; set; }

        /// <summary>
        /// Unknown conditions for connectors.
        /// </summary>
        public ConnectorConditionSection ConnectorConditions { get; set; }

        /// <summary>
        /// Unknown.
        /// </summary>
        public Section7 Entries7 { get; set; }

        /// <summary>
        /// Unknown.
        /// </summary>
        public Section8 Entries8 { get; set; }

        /// <summary>
        /// Creates an empty NVA formatted for DS3.
        /// </summary>
        public NVA()
        {
            Version = NVAVersion.DarkSouls3;
            Navmeshes = new NavmeshSection(2);
            Entries1 = new Section1();
            Entries2 = new Section2();
            Connectors = new ConnectorSection();
            ConnectorPoints = new ConnectorPointSection();
            ConnectorConditions = new ConnectorConditionSection();
            Entries7 = new Section7();
            Entries8 = new Section8(1);
        }

        internal override bool Is(BinaryReaderEx br)
        {
            string magic = br.GetASCII(0, 4);
            return magic == "NVMA";
        }

        internal override void Read(BinaryReaderEx br)
        {
            br.BigEndian = false;
            br.AssertASCII("NVMA");
            Version = br.ReadEnum32<NVAVersion>();
            br.ReadUInt32(); // File size
            br.AssertInt32(Version == NVAVersion.OldBloodborne ? 8 : 9); // Section count

            Navmeshes = new NavmeshSection(br);
            Entries1 = new Section1(br);
            Entries2 = new Section2(br);
            new Section3(br);
            Connectors = new ConnectorSection(br);
            ConnectorPoints = new ConnectorPointSection(br);
            ConnectorConditions = new ConnectorConditionSection(br);
            Entries7 = new Section7(br);
            if (Version == NVAVersion.OldBloodborne)
                Entries8 = new Section8(1);
            else
                Entries8 = new Section8(br);
        }

        internal override void Write(BinaryWriterEx bw)
        {
            bw.BigEndian = false;
            bw.WriteASCII("NVMA");
            bw.WriteUInt32((uint)Version);
            bw.ReserveUInt32("FileSize");
            bw.WriteInt32(Version == NVAVersion.OldBloodborne ? 8 : 9);

            Navmeshes.Write(bw, 0);
            Entries1.Write(bw, 1);
            Entries2.Write(bw, 2);
            new Section3().Write(bw, 3);
            Connectors.Write(bw, 4);
            ConnectorPoints.Write(bw, 5);
            ConnectorConditions.Write(bw, 6);
            Entries7.Write(bw, 7);
            if (Version != NVAVersion.OldBloodborne)
                Entries8.Write(bw, 8);
            bw.FillUInt32("FileSize", (uint)bw.Position);
        }

        /// <summary>
        /// NVA is split up into 8 lists of different types.
        /// </summary>
        public abstract class Section<T> : List<T>
        {
            /// <summary>
            /// A version number indicating the format of the section. Don't change this unless you know what you're doing.
            /// </summary>
            public int Version { get; set; }

            internal Section(int version) : base()
            {
                Version = version;
            }

            internal Section(BinaryReaderEx br, int index, params int[] versions) : base()
            {
                br.AssertInt32(index);
                Version = br.AssertInt32(versions);
                int length = br.ReadInt32();
                int count = br.ReadInt32();
                Capacity = count;

                long start = br.Position;
                ReadEntries(br, count);
                br.Position = start + length;
            }

            internal abstract void ReadEntries(BinaryReaderEx br, int count);

            internal void Write(BinaryWriterEx bw, int index)
            {
                bw.WriteInt32(index);
                bw.WriteInt32(Version);
                bw.ReserveInt32("SectionLength");
                bw.WriteInt32(Count);

                long start = bw.Position;
                WriteEntries(bw);
                if (bw.Position % 0x10 != 0)
                    bw.WritePattern(0x10 - (int)bw.Position % 0x10, 0xFF);
                bw.FillInt32("SectionLength", (int)(bw.Position - start));
            }

            internal abstract void WriteEntries(BinaryWriterEx bw);
        }

        /// <summary>
        /// A list of navmesh instances. Version: 2 for DS3 and the BB test map, 3 for BB, 4 for Sekiro.
        /// </summary>
        public class NavmeshSection : Section<Navmesh>
        {
            /// <summary>
            /// Creates an empty NavmeshSection with the given version.
            /// </summary>
            public NavmeshSection(int version) : base(version) { }

            internal NavmeshSection(BinaryReaderEx br) : base(br, 0, 2, 3, 4) { }

            internal override void ReadEntries(BinaryReaderEx br, int count)
            {
                for (int i = 0; i < count; i++)
                    Add(new Navmesh(br, Version));
            }

            internal override void WriteEntries(BinaryWriterEx bw)
            {
                for (int i = 0; i < Count; i++)
                    this[i].Write(bw, Version, i);

                for (int i = 0; i < Count; i++)
                    this[i].WriteNameRefs(bw, Version, i);
            }
        }

        /// <summary>
        /// An instance of a navmesh.
        /// </summary>
        public class Navmesh
        {
            /// <summary>
            /// Position of the mesh.
            /// </summary>
            public Vector3 Position { get; set; }

            /// <summary>
            /// Rotation of the mesh, in radians.
            /// </summary>
            public Vector3 Rotation { get; set; }

            /// <summary>
            /// Scale of the mesh.
            /// </summary>
            public Vector3 Scale { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int NameID { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int ModelID { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int Unk38 { get; set; }

            /// <summary>
            /// Should equal number of vertices in the model file.
            /// </summary>
            public int VertexCount { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public List<int> NameReferenceIDs { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public short Unk48 { get; set; }

            /// <summary>
            /// Unknown; Pav says it's a count of something.
            /// </summary>
            public short Unk4A { get; set; }

            /// <summary>
            /// Unknown
            /// </summary>
            public bool Unk4C { get; set; }

            /// <summary>
            /// Creates a Navmesh with default values.
            /// </summary>
            public Navmesh()
            {
                Scale = Vector3.One;
                NameReferenceIDs = new List<int>();
            }

            internal Navmesh(BinaryReaderEx br, int version)
            {
                Position = br.ReadVector3();
                br.AssertSingle(1);
                Rotation = br.ReadVector3();
                br.AssertInt32(0);
                Scale = br.ReadVector3();
                br.AssertInt32(0);
                NameID = br.ReadInt32();
                ModelID = br.ReadInt32();
                Unk38 = br.ReadInt32();
                br.AssertInt32(0);
                VertexCount = br.ReadInt32();
                int nameRefCount = br.ReadInt32();
                Unk48 = br.ReadInt16();
                Unk4A = br.ReadInt16();
                Unk4C = br.AssertInt32(0, 1) == 1;

                if (version < 4)
                {
                    if (nameRefCount > 16)
                        throw new InvalidDataException("Name reference count should not exceed 16 in DS3/BB.");
                    NameReferenceIDs = new List<int>(br.ReadInt32s(nameRefCount));
                    for (int i = 0; i < 16 - nameRefCount; i++)
                        br.AssertInt32(-1);
                }
                else
                {
                    int nameRefOffset = br.ReadInt32();
                    br.AssertInt32(0);
                    br.AssertInt32(0);
                    br.AssertInt32(0);
                    NameReferenceIDs = new List<int>(br.GetInt32s(nameRefOffset, nameRefCount));
                }
            }

            internal void Write(BinaryWriterEx bw, int version, int index)
            {
                bw.WriteVector3(Position);
                bw.WriteSingle(1);
                bw.WriteVector3(Rotation);
                bw.WriteInt32(0);
                bw.WriteVector3(Scale);
                bw.WriteInt32(0);
                bw.WriteInt32(NameID);
                bw.WriteInt32(ModelID);
                bw.WriteInt32(Unk38);
                bw.WriteInt32(0);
                bw.WriteInt32(VertexCount);
                bw.WriteInt32(NameReferenceIDs.Count);
                bw.WriteInt16(Unk48);
                bw.WriteInt16(Unk4A);
                bw.WriteInt32(Unk4C ? 1 : 0);

                if (version < 4)
                {
                    if (NameReferenceIDs.Count > 16)
                        throw new InvalidDataException("Name reference count should not exceed 16 in DS3/BB.");
                    bw.WriteInt32s(NameReferenceIDs);
                    for (int i = 0; i < 16 - NameReferenceIDs.Count; i++)
                        bw.WriteInt32(-1);
                }
                else
                {
                    bw.ReserveInt32($"NameRefOffset{index}");
                    bw.WriteInt32(0);
                    bw.WriteInt32(0);
                    bw.WriteInt32(0);
                }
            }

            internal void WriteNameRefs(BinaryWriterEx bw, int version, int index)
            {
                if (version >= 4)
                {
                    bw.FillInt32($"NameRefOffset{index}", (int)bw.Position);
                    bw.WriteInt32s(NameReferenceIDs);
                }
            }
        }

        /// <summary>
        /// Unknown.
        /// </summary>
        public class Section1 : Section<Entry1>
        {
            /// <summary>
            /// Creates an empty Section1.
            /// </summary>
            public Section1() : base(1) { }

            internal Section1(BinaryReaderEx br) : base(br, 1, 1) { }

            internal override void ReadEntries(BinaryReaderEx br, int count)
            {
                for (int i = 0; i < count; i++)
                    Add(new Entry1(br));
            }

            internal override void WriteEntries(BinaryWriterEx bw)
            {
                foreach (Entry1 entry in this)
                    entry.Write(bw);
            }
        }

        /// <summary>
        /// Unknown.
        /// </summary>
        public class Entry1
        {
            /// <summary>
            /// Unknown; always 0 in DS3 and SDT, sometimes 1 in BB.
            /// </summary>
            public int Unk00 { get; set; }

            /// <summary>
            /// Creates an Entry1 with default values.
            /// </summary>
            public Entry1() { }

            internal Entry1(BinaryReaderEx br)
            {
                Unk00 = br.ReadInt32();
                br.AssertInt32(0);
            }

            internal void Write(BinaryWriterEx bw)
            {
                bw.WriteInt32(Unk00);
                bw.WriteInt32(0);
            }
        }

        /// <summary>
        /// Unknown.
        /// </summary>
        public class Section2 : Section<Entry2>
        {
            /// <summary>
            /// Creates an empty Section2.
            /// </summary>
            public Section2() : base(1) { }

            internal Section2(BinaryReaderEx br) : base(br, 2, 1) { }

            internal override void ReadEntries(BinaryReaderEx br, int count)
            {
                for (int i = 0; i < count; i++)
                    Add(new Entry2(br));
            }

            internal override void WriteEntries(BinaryWriterEx bw)
            {
                foreach (Entry2 entry in this)
                    entry.Write(bw);
            }
        }

        /// <summary>
        /// Unknown.
        /// </summary>
        public class Entry2
        {
            /// <summary>
            /// Unknown; seems to just be the index of this entry.
            /// </summary>
            public int Unk00 { get; set; }

            /// <summary>
            /// References in this entry; maximum of 64.
            /// </summary>
            public List<Reference> References { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int Unk08 { get; set; }

            /// <summary>
            /// Creates an Entry2 with default values.
            /// </summary>
            public Entry2()
            {
                References = new List<Reference>();
                Unk08 = -1;
            }

            internal Entry2(BinaryReaderEx br)
            {
                Unk00 = br.ReadInt32();
                int referenceCount = br.ReadInt32();
                Unk08 = br.ReadInt32();
                br.AssertInt32(0);
                if (referenceCount > 64)
                    throw new InvalidDataException("Entry2 reference count should not exceed 64.");

                References = new List<Reference>(referenceCount);
                for (int i = 0; i < referenceCount; i++)
                    References.Add(new Reference(br));

                for (int i = 0; i < 64 - referenceCount; i++)
                    br.AssertInt64(0);
            }

            internal void Write(BinaryWriterEx bw)
            {
                bw.WriteInt32(Unk00);
                bw.WriteInt32(References.Count);
                bw.WriteInt32(Unk08);
                bw.WriteInt32(0);
                if (References.Count > 64)
                    throw new InvalidDataException("Entry2 reference count should not exceed 64.");

                foreach (Reference reference in References)
                    reference.Write(bw);

                for (int i = 0; i < 64 - References.Count; i++)
                    bw.WriteInt64(0);
            }

            /// <summary>
            /// Unknown.
            /// </summary>
            public class Reference
            {
                /// <summary>
                /// Unknown.
                /// </summary>
                public int UnkIndex { get; set; }

                /// <summary>
                /// Unknown.
                /// </summary>
                public int NameID { get; set; }

                /// <summary>
                /// Creates a Reference with defalt values.
                /// </summary>
                public Reference() { }

                internal Reference(BinaryReaderEx br)
                {
                    UnkIndex = br.ReadInt32();
                    NameID = br.ReadInt32();
                }

                internal void Write(BinaryWriterEx bw)
                {
                    bw.WriteInt32(UnkIndex);
                    bw.WriteInt32(NameID);
                }
            }
        }

        private class Section3 : Section<Entry3>
        {
            public Section3() : base(1) { }

            internal Section3(BinaryReaderEx br) : base(br, 3, 1) { }

            internal override void ReadEntries(BinaryReaderEx br, int count)
            {
                for (int i = 0; i < count; i++)
                    Add(new Entry3(br));
            }

            internal override void WriteEntries(BinaryWriterEx bw)
            {
                foreach (Entry3 entry in this)
                    entry.Write(bw);
            }
        }

        private class Entry3
        {
            internal Entry3(BinaryReaderEx br)
            {
                throw new NotImplementedException("Section3 is empty in all known NVAs.");
            }

            internal void Write(BinaryWriterEx bw)
            {
                throw new NotImplementedException("Section3 is empty in all known NVAs.");
            }
        }

        /// <summary>
        /// A list of connections between navmeshes.
        /// </summary>
        public class ConnectorSection : Section<Connector>
        {
            /// <summary>
            /// Creates an empty ConnectorSection.
            /// </summary>
            public ConnectorSection() : base(1) { }

            internal ConnectorSection(BinaryReaderEx br) : base(br, 4, 1) { }

            internal override void ReadEntries(BinaryReaderEx br, int count)
            {
                for (int i = 0; i < count; i++)
                    Add(new Connector(br));
            }

            internal override void WriteEntries(BinaryWriterEx bw)
            {
                foreach (Connector entry in this)
                    entry.Write(bw);
            }
        }

        /// <summary>
        /// A connection between two navmeshes.
        /// </summary>
        public class Connector
        {
            /// <summary>
            /// Unknown.
            /// </summary>
            public int MainNameID { get; set; }

            /// <summary>
            /// The navmesh to be attached.
            /// </summary>
            public int TargetNameID { get; set; }

            /// <summary>
            /// Index to another connector.
            /// </summary>
            public int Section4Index { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int SelfCompareCondition { get; set; }

            /// <summary>
            /// Index to a connector point.
            /// </summary>
            public int Section5Index { get; set; }

            /// <summary>
            /// Index to a connector condition.
            /// </summary>
            public int Section6Index { get; set; }

            /// <summary>
            /// Creates a Connector with default values.
            /// </summary>
            public Connector() { }

            internal Connector(BinaryReaderEx br)
            {
                MainNameID = br.ReadInt32();
                TargetNameID = br.ReadInt32();
                Section4Index = br.ReadInt32();
                SelfCompareCondition = br.ReadInt32();
                Section5Index = br.ReadInt32();
                br.AssertInt32(0);
                Section6Index = br.ReadInt32();
                br.AssertInt32(0);
            }

            internal void Write(BinaryWriterEx bw)
            {
                bw.WriteInt32(MainNameID);
                bw.WriteInt32(TargetNameID);
                bw.WriteInt32(Section4Index);
                bw.WriteInt32(SelfCompareCondition);
                bw.WriteInt32(Section5Index);
                bw.WriteInt32(0);
                bw.WriteInt32(Section6Index);
                bw.WriteInt32(0);
            }
        }

        /// <summary>
        /// A list of points used to connect navmeshes.
        /// </summary>
        public class ConnectorPointSection : Section<ConnectorPoint>
        {
            /// <summary>
            /// Creates an empty ConnectorPointSection.
            /// </summary>
            public ConnectorPointSection() : base(1) { }

            internal ConnectorPointSection(BinaryReaderEx br) : base(br, 5, 1) { }

            internal override void ReadEntries(BinaryReaderEx br, int count)
            {
                for (int i = 0; i < count; i++)
                    Add(new ConnectorPoint(br));
            }

            internal override void WriteEntries(BinaryWriterEx bw)
            {
                foreach (ConnectorPoint entry in this)
                    entry.Write(bw);
            }
        }

        /// <summary>
        /// A point used to connect two navmeshes.
        /// </summary>
        public class ConnectorPoint
        {
            /// <summary>
            /// Unknown.
            /// </summary>
            public int Unk00 { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int Unk04 { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int Unk08 { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int Unk0C { get; set; }

            /// <summary>
            /// Creates a ConnectorPoint with default values.
            /// </summary>
            public ConnectorPoint() { }

            internal ConnectorPoint(BinaryReaderEx br)
            {
                Unk00 = br.ReadInt32();
                Unk04 = br.ReadInt32();
                Unk08 = br.ReadInt32();
                Unk0C = br.ReadInt32();
            }

            internal void Write(BinaryWriterEx bw)
            {
                bw.WriteInt32(Unk00);
                bw.WriteInt32(Unk04);
                bw.WriteInt32(Unk08);
                bw.WriteInt32(Unk0C);
            }
        }

        /// <summary>
        /// A list of unknown conditions used by connectors.
        /// </summary>
        public class ConnectorConditionSection : Section<ConnectorCondition>
        {
            /// <summary>
            /// Creates an empty ConnectorConditionSection.
            /// </summary>
            public ConnectorConditionSection() : base(1) { }

            internal ConnectorConditionSection(BinaryReaderEx br) : base(br, 6, 1) { }

            internal override void ReadEntries(BinaryReaderEx br, int count)
            {
                for (int i = 0; i < count; i++)
                    Add(new ConnectorCondition(br));
            }

            internal override void WriteEntries(BinaryWriterEx bw)
            {
                foreach (ConnectorCondition entry in this)
                    entry.Write(bw);
            }
        }

        /// <summary>
        /// An unknown condition used by a connector.
        /// </summary>
        public class ConnectorCondition
        {
            /// <summary>
            /// Unknown.
            /// </summary>
            public int Condition1 { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int Condition2 { get; set; }

            /// <summary>
            /// Creates a ConnectorCondition with default values.
            /// </summary>
            public ConnectorCondition() { }

            internal ConnectorCondition(BinaryReaderEx br)
            {
                Condition1 = br.ReadInt32();
                Condition2 = br.ReadInt32();
            }

            internal void Write(BinaryWriterEx bw)
            {
                bw.WriteInt32(Condition1);
                bw.WriteInt32(Condition2);
            }
        }

        /// <summary>
        /// Unknown.
        /// </summary>
        public class Section7 : Section<Entry7>
        {
            /// <summary>
            /// Creates an empty Section7.
            /// </summary>
            public Section7() : base(1) { }

            internal Section7(BinaryReaderEx br) : base(br, 7, 1) { }

            internal override void ReadEntries(BinaryReaderEx br, int count)
            {
                for (int i = 0; i < count; i++)
                    Add(new Entry7(br));
            }

            internal override void WriteEntries(BinaryWriterEx bw)
            {
                foreach (Entry7 entry in this)
                    entry.Write(bw);
            }
        }

        /// <summary>
        /// Unknown; believed to have something to do with connecting maps.
        /// </summary>
        public class Entry7
        {
            /// <summary>
            /// Unknown.
            /// </summary>
            public Vector3 Position { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int NameID { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public int Unk18 { get; set; }

            /// <summary>
            /// Creates an Entry7 with default values.
            /// </summary>
            public Entry7() { }

            internal Entry7(BinaryReaderEx br)
            {
                Position = br.ReadVector3();
                br.AssertSingle(1);
                NameID = br.ReadInt32();
                br.AssertInt32(0);
                Unk18 = br.ReadInt32();
                br.AssertInt32(0);
            }

            internal void Write(BinaryWriterEx bw)
            {
                bw.WriteVector3(Position);
                bw.WriteSingle(1);
                bw.WriteInt32(NameID);
                bw.WriteInt32(0);
                bw.WriteInt32(Unk18);
                bw.WriteInt32(0);
            }
        }

        /// <summary>
        /// Unknown. Version: 1 for BB and DS3, 2 for Sekiro.
        /// </summary>
        public class Section8 : Section<Entry8>
        {
            /// <summary>
            /// Creates an empty Section8 with the given version.
            /// </summary>
            public Section8(int version) : base(version) { }

            internal Section8(BinaryReaderEx br) : base(br, 8, 1, 2) { }

            internal override void ReadEntries(BinaryReaderEx br, int count)
            {
                for (int i = 0; i < count; i++)
                    Add(new Entry8(br, Version));
            }

            internal override void WriteEntries(BinaryWriterEx bw)
            {
                for (int i = 0; i < Count; i++)
                    this[i].Write(bw, Version, i);

                for (int i = 0; i < Count; i++)
                    this[i].WriteSubIDs(bw, Version, i);
            }
        }

        /// <summary>
        /// Unknown.
        /// </summary>
        public class Entry8
        {
            /// <summary>
            /// Unknown.
            /// </summary>
            public Vector3 Position { get; set; }

            /// <summary>
            /// Index to a navmesh.
            /// </summary>
            public short Section0Index { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public short MainID { get; set; }

            /// <summary>
            /// Unknown.
            /// </summary>
            public List<short> SubIDs { get; set; }

            /// <summary>
            /// Unknown; only present in Sekiro.
            /// </summary>
            public int Unk14 { get; set; }

            /// <summary>
            /// Creates an Entry8 with default values.
            /// </summary>
            public Entry8()
            {
                SubIDs = new List<short>(16);
                for (int i = 0; i < 16; i++)
                    SubIDs.Add(-1);
            }

            internal Entry8(BinaryReaderEx br, int version)
            {
                Position = br.ReadVector3();
                Section0Index = br.ReadInt16();
                MainID = br.ReadInt16();

                if (version < 2)
                {
                    SubIDs = new List<short>(br.ReadInt16s(16));
                }
                else
                {
                    int subIDCount = br.ReadInt32();
                    Unk14 = br.ReadInt32();
                    int subIDsOffset = br.ReadInt32();
                    br.AssertInt32(0);
                    SubIDs = new List<short>(br.GetInt16s(subIDsOffset, subIDCount));
                }
            }

            internal void Write(BinaryWriterEx bw, int version, int index)
            {
                bw.WriteVector3(Position);
                bw.WriteInt16(Section0Index);
                bw.WriteInt16(MainID);

                if (version < 2)
                {
                    if (SubIDs.Count != 16)
                        throw new InvalidDataException("Sub ID count must be 16 in DS3/BB.");
                    bw.WriteInt16s(SubIDs);
                }
                else
                {
                    bw.WriteInt32(SubIDs.Count);
                    bw.WriteInt32(Unk14);
                    bw.ReserveInt32($"SubIDsOffset{index}");
                    bw.WriteInt32(0);
                }
            }

            internal void WriteSubIDs(BinaryWriterEx bw, int version, int index)
            {
                if (version >= 2)
                {
                    bw.FillInt32($"SubIDsOffset{index}", (int)bw.Position);
                    bw.WriteInt16s(SubIDs);
                }
            }
        }
    }
}