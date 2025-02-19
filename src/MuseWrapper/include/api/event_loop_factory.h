// Copyright 2016 InteraXon Inc.
#pragma once

#include <memory>

namespace interaxon {
namespace bridge {

class EventLoop;

/**
* A factory for creating an EventLoop that can be used for 
* processing Actions asynchronously on a secondary thread.
*/
class EventLoopFactory {

public:
    /**
    * Creates and returns an EventLoop.
    * \return EventLoop
    */
    static std::shared_ptr<EventLoop> get_event_loop();

};

}  // namespace bridge
} // namespace interaxon
