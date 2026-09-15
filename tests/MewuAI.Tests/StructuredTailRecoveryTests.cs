// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Text.Json;
using mewu_ai_Assistant.AI;
using mewu_ai_Assistant.Models;
using Xunit;

namespace MewuAI.Tests;

public sealed class StructuredTailRecoveryTests
{
    [Theory]
    [InlineData("[oops")]
    [InlineData("[{\"x\":0.1,,\"y\":0.2}]")]
    [InlineData("[{\"text\":\"bad\\q\"}]")]
    [InlineData("[{\"x\":NaN}]")]
    [InlineData("[{")]
    public void CompleteRootAnswerSurvivesBrokenAnnotationsWithoutReplacingExistingMarks(string tail)
    {
        const string answer="完整回答：\"引号\"、😀、\\路径\n第二行";
        var value="{\"meta\":{\"answer\":\"不能提升\"},\"answer\":"+JsonSerializer.Serialize(answer)+",\"annotationMode\":\"replace\",\"annotations\":"+tail;
        var result=StructuredResponseParser.Parse(value,"独立思考",expectStructuredResponse:true);
        Assert.Equal(answer,result.Answer);
        Assert.Equal("独立思考",result.Reasoning);
        Assert.Empty(result.Annotations);
        Assert.Equal(AiAnnotationUpdateMode.Preserve,result.AnnotationUpdateMode);
    }

    [Theory]
    [InlineData("```json\n", "\n```")]
    [InlineData("```JSON ", "```")]
    [InlineData("说明前言\n", "")]
    public void ExistingVisualEnvelopesUseTheSameRecovery(string prefix,string suffix)
    {
        var value=prefix+"{\"annotationProtocol\":\"mewu.visual-annotations/1\",\"answer\":\"完整回答\",\"annotations\":[oops"+suffix;
        var result=StructuredResponseParser.Parse(value,expectStructuredResponse:true);
        Assert.Equal("完整回答",result.Answer);
        Assert.Empty(result.Annotations);
    }

    [Theory]
    [InlineData("{\"payload\":{\"answer\":\"nested\"},\"annotations\":[oops")]
    [InlineData("[{\"answer\":\"nested\",\"annotations\":[oops")]
    [InlineData("{invalid,\"answer\":\"bad prefix\",\"annotations\":[oops")]
    [InlineData("{\"answer\":\"first\",\"answer\":\"second\",\"annotations\":[oops")]
    [InlineData("{\"answer\":\"\",\"annotations\":[oops")]
    [InlineData("{\"answer\":null,\"annotations\":[oops")]
    [InlineData("{\"answer\":\"unfinished")]
    [InlineData("{\"answer\":\"bad\\q\",\"annotations\":[oops")]
    [InlineData("{\"answer\":\"partial\"unescaped\",\"annotations\":[oops")]
    [InlineData("{\"answer\":\"valid\",\"other\":[oops")]
    [InlineData("{\"answer\":\"example\",\"annotations\":[]} prose after a JSON example")]
    public void InvalidOrAmbiguousAnswerIsNotPromoted(string value)
    {
        var result=StructuredResponseParser.Parse(value,expectStructuredResponse:true);
        Assert.Empty(result.Answer);
        Assert.Empty(result.Annotations);
    }

    [Fact]
    public void OrdinaryProseAndMalformedExamplesAreNotChangedByTheNewFallback()
    {
        const string example="{\"answer\":\"example\",\"annotations\":[oops";
        Assert.Equal(example,StructuredResponseParser.Parse(example).Answer);
        const string prose="Here is a JSON example: "+example;
        Assert.Equal(prose,StructuredResponseParser.Parse(prose,expectStructuredResponse:true).Answer);
    }

    [Fact]
    public void UnknownProtocolCannotRecoverExecutableAnnotations()
    {
        var result=StructuredResponseParser.Parse("{\"annotationProtocol\":\"mewu.visual-annotations/99\",\"answer\":\"answer\",\"annotations\":[oops",expectStructuredResponse:true);
        Assert.Equal("answer",result.Answer);
        Assert.Empty(result.Annotations);
        Assert.Equal(AiAnnotationUpdateMode.Preserve,result.AnnotationUpdateMode);
    }

    [Fact]
    public void RecoveryWorkRemainsBounded()
    {
        var value="{\"answer\":\""+new string('x',1024*1024)+"\",\"annotations\":[oops";
        Assert.Empty(StructuredResponseParser.Parse(value,expectStructuredResponse:true).Answer);
    }
}
