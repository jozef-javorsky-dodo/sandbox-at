﻿//  Copyright 2019 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;

namespace NtApiDotNet.Ndr
{
#pragma warning disable 1591
    public enum NdrExpressionType
    {
        FC_EXPR_CONST32 = 0x01,
        FC_EXPR_CONST64 = 0x02,
        FC_EXPR_VAR = 0x03,
        FC_EXPR_OPER = 0x04,
        FC_EXPR_PAD = 0x05,
    }

    public enum NdrExpressionOperator
    {
        OP_UNARY_PLUS = 0x01,
        OP_UNARY_MINUS = 0x02,
        OP_UNARY_NOT = 0x03,
        OP_UNARY_COMPLEMENT = 0x04,
        OP_UNARY_INDIRECTION = 0x05,
        OP_UNARY_CAST = 0x06,
        OP_UNARY_AND = 0x07,
        OP_UNARY_SIZEOF = 0x08,
        OP_UNARY_ALIGNOF = 0x09,
        OP_PRE_INCR = 0x0a,
        OP_PRE_DECR = 0x0b,
        OP_POST_INCR = 0x0c,
        OP_POST_DECR = 0x0d,
        OP_PLUS = 0x0e,
        OP_MINUS = 0x0f,
        OP_STAR = 0x10,
        OP_SLASH = 0x11,
        OP_MOD = 0x12,
        OP_LEFT_SHIFT = 0x13,
        OP_RIGHT_SHIFT = 0x14,
        OP_LESS = 0x15,
        OP_LESS_EQUAL = 0x16,
        OP_GREATER_EQUAL = 0x17,
        OP_GREATER = 0x18,
        OP_EQUAL = 0x19,
        OP_NOT_EQUAL = 0x1A,
        OP_AND = 0x1B,
        OP_OR = 0x1C,
        OP_XOR = 0x1D,
        OP_LOGICAL_AND = 0x1E,
        OP_LOGICAL_OR = 0x1F,
        OP_EXPRESSION = 0x20,
        OP_ASYNCSPLIT = 0x2B,
        OP_CORR_POINTER = 0x2C,
        OP_CORR_TOP_LEVEL = 0x2D,
    }

#pragma warning restore 1591

    /// <summary>
    /// Expression element.
    /// </summary>
    [Serializable]
    public class NdrExpression
    {
        #region Private Members

        private static bool IsValidType(NdrExpressionType type)
        {
            switch (type)
            {
                case NdrExpressionType.FC_EXPR_OPER:
                case NdrExpressionType.FC_EXPR_CONST32:
                case NdrExpressionType.FC_EXPR_CONST64:
                case NdrExpressionType.FC_EXPR_VAR:
                    return true;
            }
            return false;
        }

        private static NdrExpression ReadElement(BinaryReader reader)
        {
            NdrExpressionType type = (NdrExpressionType)reader.ReadByte();
            switch (type)
            {
                case NdrExpressionType.FC_EXPR_OPER:
                    return new NdrOperatorExpression(reader);
                case NdrExpressionType.FC_EXPR_CONST32:
                case NdrExpressionType.FC_EXPR_CONST64:
                    return new NdrConstantExpression(type, reader);
                case NdrExpressionType.FC_EXPR_VAR:
                    return new NdrVariableExpression(reader);
                default:
                    break;
            }

            return new NdrExpression();
        }

        #endregion

        #region Constructors

        internal NdrExpression(NdrExpressionType type)
        {
            Type = type;
        }

        internal NdrExpression() : this(0)
        {
        }

        #endregion

        /// <summary>
        /// The expression type.
        /// </summary>
        public NdrExpressionType Type { get; }

        /// <summary>
        /// Is this operator element valid.
        /// </summary>
        public bool IsValid { get; internal set; }

        internal static NdrExpression ReadExpression(BinaryReader reader, NdrParseContext context)
        {
            NdrExpression element = ReadElement(reader);
            if (!element.IsValid)
            {
                return element;
            }

            if ((element is NdrVariableExpression) || (element is NdrConstantExpression))
            {
                return element;
            }

            NdrOperatorExpression op_expr = (NdrOperatorExpression)element;
            for (int i = 0; i < op_expr.ArgumentsTotal; ++i)
            {
                op_expr.Arguments.Add(ReadExpression(reader, context));
            }

            return op_expr;
        }

        internal static NdrExpression Read(NdrParseContext context, int index)
        {
            if (context.ExprDesc.pOffset == IntPtr.Zero || context.ExprDesc.pFormatExpr == IntPtr.Zero || index < 0)
            {
                return new NdrExpression(0);
            }

            int expr_ofs = context.Reader.ReadInt16(context.ExprDesc.pOffset + (2 * index));
            if (expr_ofs < 0)
            {
                return new NdrExpression(0);
            }

            BinaryReader reader = context.Reader.GetReader(context.ExprDesc.pFormatExpr + expr_ofs);
            return ReadExpression(reader, context);
        }
    }

    /// <summary>
    /// Operator expression element.
    /// </summary>
    [Serializable]
    public sealed class NdrOperatorExpression : NdrExpression
    {
        /// <summary>
        /// NDR format type of element.
        /// </summary>
        public NdrExpressionOperator Operator { get; }

        /// <summary>
        /// NDR format type of element.
        /// </summary>
        public NdrFormatCharacter Format { get; }

        /// <summary>
        /// Offset, used for OP_EXPRESSION.
        /// </summary>
        public int Offset { get; }

        internal int ArgumentsTotal { get; }

        /// <summary>
        /// Parsed arguments.
        /// </summary>
        public List<NdrExpression> Arguments { get; private set; }

        internal void SetArguments(Stack<NdrExpression> elements)
        {
            List<NdrExpression> args = new List<NdrExpression>();
            for (int i = 0; i < ArgumentsTotal; ++i)
            {
                args.Insert(0, elements.Pop());
            }
            Arguments = args;
        }

        internal NdrOperatorExpression(BinaryReader reader) 
            : base(NdrExpressionType.FC_EXPR_VAR)
        {
            Operator = (NdrExpressionOperator)reader.ReadByte();
            Offset = reader.ReadInt16();
            Format = (NdrFormatCharacter)(Offset & 0xFF);
            Arguments = new List<NdrExpression>();

            switch (Operator)
            {
                case NdrExpressionOperator.OP_UNARY_INDIRECTION:
                case NdrExpressionOperator.OP_UNARY_MINUS:
                case NdrExpressionOperator.OP_UNARY_PLUS:
                case NdrExpressionOperator.OP_UNARY_CAST:
                case NdrExpressionOperator.OP_UNARY_COMPLEMENT:
                case NdrExpressionOperator.OP_UNARY_NOT:
                case NdrExpressionOperator.OP_UNARY_SIZEOF:
                case NdrExpressionOperator.OP_UNARY_ALIGNOF:
                case NdrExpressionOperator.OP_UNARY_AND:
                    IsValid = true;
                    ArgumentsTotal = 1;
                    break;
                case NdrExpressionOperator.OP_MINUS:
                case NdrExpressionOperator.OP_MOD:
                case NdrExpressionOperator.OP_OR:
                case NdrExpressionOperator.OP_PLUS:
                case NdrExpressionOperator.OP_SLASH:
                case NdrExpressionOperator.OP_STAR:
                case NdrExpressionOperator.OP_XOR:
                case NdrExpressionOperator.OP_AND:
                case NdrExpressionOperator.OP_LEFT_SHIFT:
                case NdrExpressionOperator.OP_RIGHT_SHIFT:
                case NdrExpressionOperator.OP_EQUAL:
                case NdrExpressionOperator.OP_GREATER:
                case NdrExpressionOperator.OP_GREATER_EQUAL:
                case NdrExpressionOperator.OP_LESS:
                case NdrExpressionOperator.OP_LESS_EQUAL:
                case NdrExpressionOperator.OP_LOGICAL_AND:
                case NdrExpressionOperator.OP_LOGICAL_OR:
                case NdrExpressionOperator.OP_NOT_EQUAL:
                    IsValid = true;
                    ArgumentsTotal = 2;
                    break;
                case NdrExpressionOperator.OP_EXPRESSION:
                    IsValid = true;
                    ArgumentsTotal = 3;
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Variable expression element.
    /// </summary>
    [Serializable]
    public sealed class NdrVariableExpression : NdrExpression
    {
        /// <summary>
        /// Offset of the variable.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// NDR format type of element.
        /// </summary>
        public NdrFormatCharacter Format { get; }

        internal NdrVariableExpression(BinaryReader reader)
            : base(NdrExpressionType.FC_EXPR_VAR)
        {
            Format = (NdrFormatCharacter)reader.ReadByte();
            Offset = reader.ReadInt16();
            IsValid = true;
        }
    }

    /// <summary>
    /// Expression element.
    /// </summary>
    [Serializable]
    public sealed class NdrConstantExpression : NdrExpression
    {
        /// <summary>
        /// NDR format type of element.
        /// </summary>
        public NdrFormatCharacter Format { get; }

        /// <summary>
        /// Offset of the variable.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// The value of the constant.
        /// </summary>
        public long Value { get; }

        internal NdrConstantExpression(NdrExpressionType type, BinaryReader reader)
            : base(type)
        {
            Format = (NdrFormatCharacter)reader.ReadByte();
            Offset = reader.ReadInt16();
            if (type == NdrExpressionType.FC_EXPR_CONST32)
            {
                Value = reader.ReadInt32();
            }
            else
            {
                Value = reader.ReadInt64();
            }
            IsValid = true;
        }
    }
}
