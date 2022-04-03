namespace Kairos.Server.Tests

open Expecto
open Kairos.Server

type IntEvent = Inc

module EventBusTests =
  let allTests =
    testList "Event bus" [
      testAsync "Test" {
        Expect.equal 1 1 "ToDo"
      }
    ]

