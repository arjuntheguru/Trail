import { calendarActions } from "./calendar-slice";
import { tokenConfig } from "../utils/tokenUtility";

import API from '../utils/api';

export const getCalendar = (siteId, date) => (dispatch) => {

    dispatch(calendarActions.start_loading());
    let config = tokenConfig();

    API.get(`api/calendar/${siteId}/${date}`, config)
        .then(res => {
            dispatch(calendarActions.load_calendar_success(res.data));
        })
        .catch(err => {
            console.log(err.response);
            dispatch(calendarActions.error());
        });
};

export const updateTaskItemStatus = (item) => (dispatch) => {

    // dispatch(calendarActions.start_minor_loading());
    let config = tokenConfig();

    API.patch(`api/calendar/updateTaskItemStatus`, item , config)
        .then(res => {
            // return "Success"
            dispatch(calendarActions.update_calendar_success(res.data));
        })
        .catch(err => {
            console.log(err.response);
            // dispatch(calendarActions.error());
        });
};

export const updateTaskListStatus = (item) => (dispatch) => {

    dispatch(calendarActions.start_loading());
    let config = tokenConfig();

    API.put(`api/calendar/updateTaskListStatus`, item , config)
        .then(res => {
            dispatch(calendarActions.update_calendar_success(res.data));
        })
        .catch(err => {
            console.log(err.response);
            dispatch(calendarActions.error());
        });
};

export const insertComment = (item) => (dispatch) => {

    dispatch(calendarActions.start_loading());
    let config = tokenConfig();

    API.put(`api/calendar/comment`, item , config)
        .then(res => {
            dispatch(calendarActions.update_calendar_success(res.data));
        })
        .catch(err => {
            console.log(err.response);
            dispatch(calendarActions.error());
        });
};

