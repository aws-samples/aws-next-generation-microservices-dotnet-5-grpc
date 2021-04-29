// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: submit_order.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace ModernTacoShop.SubmitOrder.Protos {

  /// <summary>Holder for reflection information generated from submit_order.proto</summary>
  public static partial class SubmitOrderReflection {

    #region Descriptor
    /// <summary>File descriptor for submit_order.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static SubmitOrderReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChJzdWJtaXRfb3JkZXIucHJvdG8SEG1vZGVybl90YWNvX3Nob3AaG2dvb2ds",
            "ZS9wcm90b2J1Zi9lbXB0eS5wcm90bxocZ29vZ2xlL3Byb3RvYnVmL3N0cnVj",
            "dC5wcm90bxofZ29vZ2xlL3Byb3RvYnVmL3RpbWVzdGFtcC5wcm90byItCgVP",
            "cmRlchIQCghvcmRlcl9pZBgBIAEoAxISCgpvcmRlcl9qc29uGAYgASgJMowB",
            "CgtTdWJtaXRPcmRlchI+CgtTdWJtaXRPcmRlchIXLm1vZGVybl90YWNvX3No",
            "b3AuT3JkZXIaFi5nb29nbGUucHJvdG9idWYuRW1wdHkSPQoLSGVhbHRoQ2hl",
            "Y2sSFi5nb29nbGUucHJvdG9idWYuRW1wdHkaFi5nb29nbGUucHJvdG9idWYu",
            "RW1wdHlCJKoCIU1vZGVyblRhY29TaG9wLlN1Ym1pdE9yZGVyLlByb3Rvc2IG",
            "cHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Google.Protobuf.WellKnownTypes.EmptyReflection.Descriptor, global::Google.Protobuf.WellKnownTypes.StructReflection.Descriptor, global::Google.Protobuf.WellKnownTypes.TimestampReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::ModernTacoShop.SubmitOrder.Protos.Order), global::ModernTacoShop.SubmitOrder.Protos.Order.Parser, new[]{ "OrderId", "OrderJson" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class Order : pb::IMessage<Order>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<Order> _parser = new pb::MessageParser<Order>(() => new Order());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<Order> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::ModernTacoShop.SubmitOrder.Protos.SubmitOrderReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public Order() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public Order(Order other) : this() {
      orderId_ = other.orderId_;
      orderJson_ = other.orderJson_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public Order Clone() {
      return new Order(this);
    }

    /// <summary>Field number for the "order_id" field.</summary>
    public const int OrderIdFieldNumber = 1;
    private long orderId_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public long OrderId {
      get { return orderId_; }
      set {
        orderId_ = value;
      }
    }

    /// <summary>Field number for the "order_json" field.</summary>
    public const int OrderJsonFieldNumber = 6;
    private string orderJson_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string OrderJson {
      get { return orderJson_; }
      set {
        orderJson_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as Order);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(Order other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (OrderId != other.OrderId) return false;
      if (OrderJson != other.OrderJson) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (OrderId != 0L) hash ^= OrderId.GetHashCode();
      if (OrderJson.Length != 0) hash ^= OrderJson.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (OrderId != 0L) {
        output.WriteRawTag(8);
        output.WriteInt64(OrderId);
      }
      if (OrderJson.Length != 0) {
        output.WriteRawTag(50);
        output.WriteString(OrderJson);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (OrderId != 0L) {
        output.WriteRawTag(8);
        output.WriteInt64(OrderId);
      }
      if (OrderJson.Length != 0) {
        output.WriteRawTag(50);
        output.WriteString(OrderJson);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (OrderId != 0L) {
        size += 1 + pb::CodedOutputStream.ComputeInt64Size(OrderId);
      }
      if (OrderJson.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(OrderJson);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(Order other) {
      if (other == null) {
        return;
      }
      if (other.OrderId != 0L) {
        OrderId = other.OrderId;
      }
      if (other.OrderJson.Length != 0) {
        OrderJson = other.OrderJson;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 8: {
            OrderId = input.ReadInt64();
            break;
          }
          case 50: {
            OrderJson = input.ReadString();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 8: {
            OrderId = input.ReadInt64();
            break;
          }
          case 50: {
            OrderJson = input.ReadString();
            break;
          }
        }
      }
    }
    #endif

  }

  #endregion

}

#endregion Designer generated code
