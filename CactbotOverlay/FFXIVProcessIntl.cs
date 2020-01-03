﻿using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace Cactbot {
  public class FFXIVProcessIntl : FFXIVProcess {

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct EntityMemory {
      public static int Size => Marshal.SizeOf(typeof(EntityMemory));

      // Unknown size, but this is the bytes up to the next field.
      public const int nameBytes = 68;

      [FieldOffset(0x30)]
      public fixed byte Name[nameBytes];

      [FieldOffset(0x74)]
      public uint id;

      [FieldOffset(0x8C)]
      public EntityType type;

      [FieldOffset(0x92)]
      public ushort distance;

      [FieldOffset(0xA0)]
      public Single pos_x;

      [FieldOffset(0xA4)]
      public Single pos_z;

      [FieldOffset(0xA8)]
      public Single pos_y;

      [FieldOffset(0xB0)]
      public Single rotation;

      [FieldOffset(0x18B8)]
      public CharacterDetails charDetails;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct CharacterDetails {

      [FieldOffset(0x00)]
      public int hp;

      [FieldOffset(0x04)]
      public int max_hp;

      [FieldOffset(0x08)]
      public int mp;

      [FieldOffset(0x0C)]
      public int max_mp;

      [FieldOffset(0x12)]
      public short gp;

      [FieldOffset(0x14)]
      public short max_gp;

      [FieldOffset(0x16)]
      public short cp;

      [FieldOffset(0x18)]
      public short max_cp;

      [FieldOffset(0x3C)]
      public EntityJob job;

      [FieldOffset(0x3E)]
      public byte level;

      [FieldOffset(0x5F)]
      public short shieldPercentage;
    }
    public FFXIVProcessIntl(ILogger logger) : base(logger) { }

    public unsafe override EntityData GetEntityDataFromByteArray(byte[] source) {
      fixed (byte* p = source) {
        EntityMemory mem = *(EntityMemory*)&p[0];

        EntityData entity = new EntityData() {
          //dump '\0' string terminators
          name = System.Text.Encoding.UTF8.GetString(mem.Name, EntityMemory.nameBytes).Split(new[] { '\0' }, 2)[0],
          id = mem.id,
          type = mem.type,
          distance = mem.distance,
          pos_x = mem.pos_x,
          pos_y = mem.pos_y,
          pos_z = mem.pos_z,
          rotation = mem.rotation,
        };
        if (entity.type == EntityType.PC || entity.type == EntityType.Monster) {
          entity.job = mem.charDetails.job;

          entity.hp = mem.charDetails.hp;
          entity.max_hp = mem.charDetails.max_hp;
          entity.mp = mem.charDetails.mp;
          entity.max_mp = mem.charDetails.max_mp;
          entity.shield_value = mem.charDetails.shieldPercentage * entity.max_hp / 100;

          if (IsGatherer(entity.job)) {
            entity.gp = mem.charDetails.gp;
            entity.max_gp = mem.charDetails.max_gp;
          }
          if (IsCrafter(entity.job)) {
            entity.cp = mem.charDetails.cp;
            entity.max_cp = mem.charDetails.max_cp;
          }

          entity.level = mem.charDetails.level;

          byte[] job_bytes = GetRawJobSpecificDataBytes();
          if (job_bytes != null) {
            for (var i = 0; i < job_bytes.Length; ++i) {
              if (entity.debug_job != "")
                entity.debug_job += " ";
              entity.debug_job += string.Format("{0:x2}", job_bytes[i]);
            }
          }
        }
        return entity;
      }
    }

    internal override EntityData GetEntityData(IntPtr entity_ptr) {
      if (entity_ptr == IntPtr.Zero)
        return null;
      byte[] source = Read8(entity_ptr, EntityMemory.Size);
      return GetEntityDataFromByteArray(source);
    }
    public override EntityData GetSelfData() {
      if (!HasProcess() || player_ptr_addr_ == IntPtr.Zero)
        return null;

      IntPtr entity_ptr = ReadIntPtr(player_ptr_addr_);
      if (entity_ptr == IntPtr.Zero)
        return null;
      var data = GetEntityData(entity_ptr);
      if (data.job == EntityJob.FSH)
        data.bait = GetBait();
      return data;
    }

    public override EntityData GetTargetData() {
      if (!HasProcess() || target_ptr_addr_ == IntPtr.Zero)
        return null;

      IntPtr entity_ptr = ReadIntPtr(target_ptr_addr_);
      if (entity_ptr == IntPtr.Zero)
        return null;
      return GetEntityData(entity_ptr);
    }

    public override EntityData GetFocusData() {
      if (!HasProcess() || focus_ptr_addr_ == IntPtr.Zero)
        return null;

      IntPtr entity_ptr = ReadIntPtr(focus_ptr_addr_);
      if (entity_ptr == IntPtr.Zero)
        return null;
      return GetEntityData(entity_ptr);
    }

    public unsafe override JObject GetJobSpecificData(EntityJob job) {
      if (!HasProcess() || job_data_outer_addr_ == IntPtr.Zero)
        return null;

      IntPtr job_inner_ptr = ReadIntPtr(job_data_outer_addr_);
      if (job_inner_ptr == IntPtr.Zero) {
        // The pointer can be null when not logged in.
        return null;
      }
      job_inner_ptr = IntPtr.Add(job_inner_ptr, kJobDataInnerStructOffset);

      fixed (byte* p = Read8(job_inner_ptr, kJobDataInnerStructSize)) {
        if (p == null)
          return null;
        else {
          switch (job) {
            case EntityJob.RDM: {
                return JObject.FromObject(*(RedMageJobMemory*)&p[0]);
              };
            case EntityJob.MRD: {
                return null;
              };
            case EntityJob.WAR: {
                return JObject.FromObject(*(WarriorJobMemory*)&p[0]);
              };
            case EntityJob.DRK: {
                return JObject.FromObject(*(DarkKnightJobMemory*)&p[0]);
              };
            case EntityJob.GLA: {
                return null;
              };
            case EntityJob.PLD: {
                return JObject.FromObject(*(PaladinJobMemory*)&p[0]);
              };
            case EntityJob.GNB: {
                return JObject.FromObject(*(GunbreakerJobMemory*)&p[0]);
              };
            case EntityJob.BRD: {
                return JObject.FromObject(*(BardJobMemory*)&p[0]);
              }
            case EntityJob.DNC: {
                return JObject.FromObject(*(DancerJobMemory*)&p[0]);
              };
            case EntityJob.LNC: {
                return null;
              };
            case EntityJob.DRG: {
                return JObject.FromObject(*(DragoonJobMemory*)&p[0]);
              };
            case EntityJob.ROG: {
                return null;
              };
            case EntityJob.NIN: {
                return JObject.FromObject(*(NinjaJobMemory*)&p[0]);
              };
            case EntityJob.THM: {
                return JObject.FromObject(*(ThaumaturgeJobMemory*)&p[0]);
              }
            case EntityJob.BLM: {
                return JObject.FromObject(*(BlackMageJobMemory*)&p[0]);
              };
            case EntityJob.CNJ: {
                return null;
              };
            case EntityJob.WHM: {
                return JObject.FromObject(*(WhiteMageJobMemory*)&p[0]);
              };
            case EntityJob.ACN: {
                return JObject.FromObject(*(ArcanistJobMemory*)&p[0]);
              };
            case EntityJob.SMN: {
                return JObject.FromObject(*(SummonerJobMemory*)&p[0]);
              };
            case EntityJob.SCH: {
                return JObject.FromObject(*(ScholarJobMemory*)&p[0]);
              };
            case EntityJob.PGL: {
                return JObject.FromObject(*(PuglistJobMemory*)&p[0]);
              };
            case EntityJob.MNK: {
                return JObject.FromObject(*(MonkJobMemory*)&p[0]);
              };
            case EntityJob.MCH: {
                return JObject.FromObject(*(MachinistJobMemory*)&p[0]);
              };
            case EntityJob.AST: {
                return JObject.FromObject(*(AstrologianJobMemory*)&p[0]);
              };
            case EntityJob.SAM: {
                return JObject.FromObject(*(SamuraiJobMemory*)&p[0]);
              };
            default: {
                logger_.LogError("GetJobSpecificData: {0}", job);
                return null;
              };
          }
        }
      }
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct RedMageJobMemory {
      [FieldOffset(0x00)]
      public byte whiteMana;

      [FieldOffset(0x01)]
      public byte blackMana;
    };

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct WarriorJobMemory {
      [FieldOffset(0x00)]
      public byte beast;
    };

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct DarkKnightJobMemory {
      [FieldOffset(0x00)]
      public byte blood;
      [FieldOffset(0x02)]
      public ushort darksideMilliseconds;
    };

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct PaladinJobMemory {
      [FieldOffset(0x00)]
      public byte oath;
    };

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct GunbreakerJobMemory {
      [FieldOffset(0x00)]
      public byte cartridges;

      [FieldOffset(0x02)]
      private ushort continuationMilliseconds; // Is 15000 if and only if continuationState is not zero.

      [FieldOffset(0x04)]
      public byte continuationState;
    };

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct BardJobMemory {
      private enum Song : byte {
        None = 0,
        Ballad = 5, // Mage's Ballad.
        Paeon = 10, // Army's Paeon.
        Minuet = 15, // The Wanderer's Minuet.
      }

      [FieldOffset(0x00)]
      public ushort songMilliseconds;

      [FieldOffset(0x02)]
      public byte songProcs;

      [FieldOffset(0x03)]
      public byte soulGauge;

      [NonSerialized]
      [FieldOffset(0x04)]
      private Song song_type;

      public String songName {
        get {
          return !Enum.IsDefined(typeof(Song), song_type) ? "None" : song_type.ToString();
        }
      }

    };

    [StructLayout(LayoutKind.Explicit)]
    public struct DancerJobMemory {
      private enum Step : byte {
        None = 0,
        Emboite = 1,
        Entrechat = 2,
        Jete = 3,
        Pirouette = 4,
      }

      [FieldOffset(0x00)]
      public byte feathers;

      [NonSerialized]
      [FieldOffset(0x02)]
      private Step step1;  // Order of steps in current Standard Step/Technical Step combo.

      [NonSerialized]
      [FieldOffset(0x03)]
      private Step step2;

      [NonSerialized]
      [FieldOffset(0x04)]
      private Step step3;

      [NonSerialized]
      [FieldOffset(0x05)]
      private Step step4;

      [FieldOffset(0x06)]
      public byte currentStep; // Number of steps executed in current Standard Step/Technical Step combo.

      public string steps {
        get {
          string _steps = step1 == Step.None ? "None" : step1.ToString();
          _steps += step2 != Step.None ? ", " + step2.ToString() : "";
          _steps += step3 != Step.None ? ", " + step3.ToString() : "";
          _steps += step4 != Step.None ? ", " + step4.ToString() : "";
          return _steps;
        }
      }
    };

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct DragoonJobMemory {
      [NonSerialized]
      [FieldOffset(0x00)]
      private ushort blood_or_life_ms;

      [NonSerialized]
      [FieldOffset(0x02)]
      private byte stance; // 0 = None, 1 = Blood, 2 = Life

      [FieldOffset(0x03)]
      public byte eyesAmount;

      public uint bloodMilliseconds {
        get {
          if (stance == 1)
            return blood_or_life_ms;
          else
            return 0;
        }
      }
      public uint lifeMilliseconds {
        get {
          if (stance == 2)
            return blood_or_life_ms;
          else
            return 0;
        }
      }
    };

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct NinjaJobMemory {
      [FieldOffset(0x00)]
      public uint hutonMilliseconds;

      [FieldOffset(0x04)]
      public byte ninkiAmount;

      [FieldOffset(0x05)]
      private byte hutonCount; // Why though?
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct ThaumaturgeJobMemory {
      [FieldOffset(0x02)]
      public ushort umbralMilliseconds; // Number of ms left in umbral fire/ice.

      [FieldOffset(0x04)]
      public sbyte umbralStacks; // Positive = Umbral Fire Stacks, Negative = Umbral Ice Stacks.
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct BlackMageJobMemory {
      [FieldOffset(0x00)]
      public ushort nextPolyglotMilliseconds; // Number of ms left before polyglot proc.

      [FieldOffset(0x02)]
      public ushort umbralMilliseconds; // Number of ms left in umbral fire/ice.

      [FieldOffset(0x04)]
      public sbyte umbralStacks; // Positive = Umbral Fire Stacks, Negative = Umbral Ice Stacks.

      [FieldOffset(0x05)]
      public byte umbralHearts;

      [FieldOffset(0x06)]
      public byte foulCount;

      [NonSerialized]
      [FieldOffset(0x07)]
      private byte enochian_state; // Bit 0 = Enochian active. Bit 1 = Polygot active.

      public bool enochian {
        get {
          return (enochian_state & 0xF) == 1;
        }
      }
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct WhiteMageJobMemory {
      [FieldOffset(0x02)]
      public ushort lilyMilliseconds; // Number of ms left before lily gain.

      [FieldOffset(0x04)]
      public byte lilyStacks;

      [FieldOffset(0x05)]
      public byte bloodlilyStacks;
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct ArcanistJobMemory {
      [FieldOffset(0x04)]
      public byte aetherflowStacks;
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct SummonerJobMemory {
      [FieldOffset(0x00)]
      public ushort stanceMilliseconds; // Dreadwyrm or Bahamut/Phoenix time left in ms.

      [FieldOffset(0x02)]
      public byte bahamutStance; // 5 if Bahamut/Phoenix summoned, else 0.

      [FieldOffset(0x03)]
      public byte bahamutSummoned; // 1 if Bahamut/Phoenix summoned, else 0.

      [NonSerialized]
      [FieldOffset(0x04)]
      private byte stacks; // Bits 1-2: Aetherflow. Bits 3-4: Dreadwyrm. Bit 5: Phoenix ready.

      public int aetherflowStacks {
        get {
          return (stacks >> 0) & 0x3; // Bottom 2 bits.
        }
      }
      public int dreadwyrmStacks {
        get {
          return (stacks >> 2) & 0x3; // Bottom 2 bits.
        }
      }
      public bool phoenixReady {
        get {
          return ((stacks >> 4) & 0x3) == 1; // Bottom 2 bits.
        }
      }
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct ScholarJobMemory {
      [FieldOffset(0x02)]
      public byte aetherflowStacks;

      [FieldOffset(0x03)]
      public byte fairyGauge;

      [FieldOffset(0x04)]
      public ushort fairyMilliseconds; // Seraph time left ms.

      [FieldOffset(0x06)]
      public byte fairyStatus; // Varies depending on which fairy was summoned, during Seraph/Dissipation: 6 - Eos, 7 - Selene, else 0.
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct PuglistJobMemory {
      [FieldOffset(0x00)]
      public ushort lightningMilliseconds;

      [FieldOffset(0x02)]
      public byte lightningStacks;
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct MonkJobMemory {
      [FieldOffset(0x00)]
      public ushort lightningMilliseconds;

      [FieldOffset(0x02)]
      public byte lightningStacks;

      [FieldOffset(0x03)]
      public byte chakraStacks;
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct MachinistJobMemory {
      [FieldOffset(0x00)]
      public ushort overheatMilliseconds;

      [FieldOffset(0x02)]
      public ushort batteryMilliseconds;

      [FieldOffset(0x04)]
      public byte heat;

      [FieldOffset(0x05)]
      public byte battery;
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct AstrologianJobMemory {
      public enum Card : byte {
        None = 0,
        Balance = 1,
        Bole = 2,
        Arrow = 3,
        Spear = 4,
        Ewer = 5,
        Spire = 6,
      }

      public enum Arcanum : byte {
        None = 0,
        Solar = 1,
        Lunar = 2,
        Celestial = 3,
      }

      [FieldOffset(0x04)]
      private Card _heldCard;

      [NonSerialized]
      [FieldOffset(0x05)]
      private Arcanum arcanum_1;

      [NonSerialized]
      [FieldOffset(0x06)]
      private Arcanum arcanum_2;

      [NonSerialized]
      [FieldOffset(0x07)]
      private Arcanum arcanum_3;

      public string heldCard {
        get {
          return _heldCard.ToString();
        }
      }

      public string arcanums {
        get {
          string _arcanums = arcanum_1 == Arcanum.None ? "None" : arcanum_1.ToString();
          _arcanums += arcanum_2 != Arcanum.None ? ", " + arcanum_2.ToString() : "";
          _arcanums += arcanum_3 != Arcanum.None ? ", " + arcanum_3.ToString() : "";
          return _arcanums;
        }
      }
    };

    [StructLayout(LayoutKind.Explicit)]
    public struct SamuraiJobMemory {
      [FieldOffset(0x04)]
      public byte kenki;

      [NonSerialized]
      [FieldOffset(0x05)]
      private byte sen_bits;

      public bool setsu {
        get {
          return (sen_bits & 0x1) != 0;
        }
      }

      public bool getsu {
        get {
          return (sen_bits & 0x2) != 0;
        }
      }

      public bool ka {
        get {
          return (sen_bits & 0x4) != 0;
        }
      }
    }
  }
}