﻿using System.Numerics;

namespace SoulsFormats
{
    public partial class FLVER
    {
        /// <summary>
        /// "Dummy polygons" used for hit detection, particle effect locations, and much more.
        /// </summary>
        public class Dummy
        {
            /// <summary>
            /// Unknown.
            /// </summary>
            public Vector3 Position;

            /// <summary>
            /// Vector indicating the dummy point's forward direction.
            /// </summary>
            public Vector3 Forward;

            /// <summary>
            /// Vector indicating the dummy point's upward direction.
            /// </summary>
            public Vector3 Upward;

            /// <summary>
            /// Indicates the type of dummy point this is (hitbox, sfx, etc).
            /// </summary>
            public short ReferenceID;

            /// <summary>
            /// Presumably the index of a bone the dummy points would be listed under in an editor. Not known to mean anything ingame.
            /// </summary>
            public short DummyBoneIndex;

            /// <summary>
            /// Index of the bone that the dummy point follows physically.
            /// </summary>
            public short AttachBoneIndex;

            /// <summary>
            /// Unknown.
            /// </summary>
            public int Unk0C;

            /// <summary>
            /// Unknown.
            /// </summary>
            public bool Flag1;
            
            /// <summary>
            /// If false, the upward vector is not read.
            /// </summary>
            public bool UseUpwardVector;

            /// <summary>
            /// Unknown; only used in Sekiro.
            /// </summary>
            public int Unk30;

            /// <summary>
            /// Unknown; only used in Sekiro.
            /// </summary>
            public int Unk34;

            /// <summary>
            /// Creates a new dummy point with default values.
            /// </summary>
            public Dummy()
            {
                DummyBoneIndex = -1;
                AttachBoneIndex = -1;
            }

            internal Dummy(BinaryReaderEx br)
            {
                Position = br.ReadVector3();
                Unk0C = br.ReadInt32();
                Forward = br.ReadVector3();
                ReferenceID = br.ReadInt16();
                DummyBoneIndex = br.ReadInt16();
                Upward = br.ReadVector3();
                AttachBoneIndex = br.ReadInt16();
                Flag1 = br.ReadBoolean();
                UseUpwardVector = br.ReadBoolean();
                Unk30 = br.ReadInt32();
                Unk34 = br.ReadInt32();
                br.AssertInt32(0);
                br.AssertInt32(0);
            }

            internal void Write(BinaryWriterEx bw)
            {
                bw.WriteVector3(Position);
                bw.WriteInt32(Unk0C);
                bw.WriteVector3(Forward);
                bw.WriteInt16(ReferenceID);
                bw.WriteInt16(DummyBoneIndex);
                bw.WriteVector3(Upward);
                bw.WriteInt16(AttachBoneIndex);
                bw.WriteBoolean(Flag1);
                bw.WriteBoolean(UseUpwardVector);
                bw.WriteInt32(Unk30);
                bw.WriteInt32(Unk34);
                bw.WriteInt32(0);
                bw.WriteInt32(0);
            }

            /// <summary>
            /// Returns the dummy point's reference ID.
            /// </summary>
            public override string ToString()
            {
                return $"{ReferenceID}";
            }
        }
    }
}
